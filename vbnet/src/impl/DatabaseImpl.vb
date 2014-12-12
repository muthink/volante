Imports System.Collections
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Threading
Imports System.Diagnostics
Imports System.Text
Imports Volante
Namespace Volante.Impl

	Public Class DatabaseImpl
		Implements IDatabase
		Public Const DEFAULT_PAGE_POOL_SIZE As Integer = 4 * 1024 * 1024

		#If CF Then
		Shared Sub New()
			assemblies = New System.Collections.ArrayList()
		End Sub
		Public Sub New(callingAssembly As Assembly)
			assemblies.Add(callingAssembly)
			assemblies.Add(Assembly.GetExecutingAssembly())
		End Sub
		#End If

        Public Property Root() As IPersistent Implements IDatabase.Root
            Get
                SyncLock Me
                    ensureOpened()
                    Dim rootOid As Integer = header.root(1 - currIndex).rootObject
                    Return If((rootOid = 0), Nothing, lookupObject(rootOid, Nothing))
                End SyncLock
            End Get

            Set(value As IPersistent)
                SyncLock Me
                    ensureOpened()
                    If Not Value.IsPersistent() Then
                        storeObject0(Value)
                    End If

                    header.root(1 - currIndex).rootObject = Value.Oid
                    modified = True
                End SyncLock
            End Set
        End Property

		''' <summary> Initialial database index size - increasing it reduce number of inde reallocation but increase
		''' initial database size. Should be set before openning connection.
		''' </summary>
		Const dbDefaultInitIndexSize As Integer = 1024

		''' <summary> Initial capacity of object hash
		''' </summary>
		Const dbDefaultObjectCacheInitSize As Integer = 1319

		''' <summary> Database extension quantum. Memory is allocated by scanning bitmap. If there is no
		''' large enough hole, then database is extended by the value of dbDefaultExtensionQuantum 
		''' This parameter should not be smaller than dbFirstUserId
		''' </summary>
		Shared dbDefaultExtensionQuantum As Long = 1024 * 1024

		Const dbDatabaseOffsetBits As Integer = 32
		' up to 4 gigabyte
		Const dbLargeDatabaseOffsetBits As Integer = 40
		' up to 1 terabyte
		Const dbAllocationQuantumBits As Integer = 5
		Const dbAllocationQuantum As Integer = 1 << dbAllocationQuantumBits
		Const dbBitmapSegmentBits As Integer = Page.pageBits + 3 + dbAllocationQuantumBits
		Const dbBitmapSegmentSize As Integer = 1 << dbBitmapSegmentBits
		Const dbBitmapPages As Integer = 1 << (dbDatabaseOffsetBits - dbBitmapSegmentBits)
		Const dbLargeBitmapPages As Integer = 1 << (dbLargeDatabaseOffsetBits - dbBitmapSegmentBits)
		Const dbHandlesPerPageBits As Integer = Page.pageBits - 3
		Const dbHandlesPerPage As Integer = 1 << dbHandlesPerPageBits
		Const dbDirtyPageBitmapSize As Integer = 1 << (32 - dbHandlesPerPageBits - 3)

		Const dbInvalidId As Integer = 0
		Const dbBitmapId As Integer = 1
		Const dbFirstUserId As Integer = dbBitmapId + dbBitmapPages

		Friend Const dbPageObjectFlag As Integer = 1
		Friend Const dbModifiedFlag As Integer = 2
		Friend Const dbFreeHandleFlag As Integer = 4
        Friend Const dbFlagsMask As UInteger = 7
        Friend Const dbFlagsBits As UInteger = 3

		Friend Sub ensureOpened()
			If Not opened Then
				Throw New DatabaseException(DatabaseException.ErrorCode.DATABASE_NOT_OPENED)
			End If
		End Sub

		Private Function getBitmapPageId(i As Integer) As Integer
			Return If(i < dbBitmapPages, dbBitmapId + i, header.root(1 - currIndex).bitmapExtent + i)
		End Function

		Friend Function getPos(oid As Integer) As Long
			SyncLock objectCache
				If oid = 0 OrElse oid >= currIndexSize Then
					Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
				End If
				Dim pg As Page = pool.getPage(header.root(1 - currIndex).index + (CLng(oid >> dbHandlesPerPageBits) << Page.pageBits))
				Dim pos As Long = Bytes.unpack8(pg.data, (oid And (dbHandlesPerPage - 1)) << 3)
				pool.unfix(pg)
				Return pos
			End SyncLock
		End Function

		Friend Sub setPos(oid As Integer, pos As Long)
			SyncLock objectCache
				dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) = dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) Or 1 << ((oid >> dbHandlesPerPageBits) And 31)
				Dim pg As Page = pool.putPage(header.root(1 - currIndex).index + (CLng(oid >> dbHandlesPerPageBits) << Page.pageBits))
				Bytes.pack8(pg.data, (oid And (dbHandlesPerPage - 1)) << 3, pos)
				pool.unfix(pg)
			End SyncLock
		End Sub

		Friend Function [get](oid As Integer) As Byte()
			Dim pos As Long = getPos(oid)
			If (pos And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
			End If

			Return pool.[get](pos And Not dbFlagsMask)
		End Function

		Friend Function getPage(oid As Integer) As Page
			Dim pos As Long = getPos(oid)
			If (pos And (dbFreeHandleFlag Or dbPageObjectFlag)) <> dbPageObjectFlag Then
				Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
			End If

			Return pool.getPage(pos And Not dbFlagsMask)
		End Function

		Friend Function putPage(oid As Integer) As Page
			SyncLock objectCache
				Dim pos As Long = getPos(oid)
				If (pos And (dbFreeHandleFlag Or dbPageObjectFlag)) <> dbPageObjectFlag Then
					Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
				End If

				If (pos And dbModifiedFlag) = 0 Then
					dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) = dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) Or 1 << ((oid >> dbHandlesPerPageBits) And 31)
					allocate(Page.pageSize, oid)
					cloneBitmap(pos And Not dbFlagsMask, Page.pageSize)
					pos = getPos(oid)
				End If
				modified = True
				Return pool.putPage(pos And Not dbFlagsMask)
			End SyncLock
		End Function

		Friend Function allocatePage() As Integer
			Dim oid As Integer = allocateId()
			setPos(oid, allocate(Page.pageSize, 0) Or dbPageObjectFlag Or dbModifiedFlag)
			Return oid
		End Function

		Public Sub deallocateObject(obj As IPersistent)
			SyncLock Me
				SyncLock objectCache
					Dim oid As Integer = obj.Oid
					If oid = 0 Then
						Return
					End If

					Dim pos As Long = getPos(oid)
					objectCache.Remove(oid)
					Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
					If (offs And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
						Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
					End If

					Dim pg As Page = pool.getPage(pos - offs)
					offs = offs And Not dbFlagsMask
					Dim size As Integer = ObjectHeader.getSize(pg.data, offs)
					pool.unfix(pg)
					freeId(oid)
					If (pos And dbModifiedFlag) <> 0 Then
						free(pos And Not dbFlagsMask, size)
					Else
						cloneBitmap(pos, size)
					End If
					obj.AssignOid(Me, 0, False)
				End SyncLock
			End SyncLock
		End Sub

		Friend Sub freePage(oid As Integer)
			Dim pos As Long = getPos(oid)
			Debug.Assert((pos And (dbFreeHandleFlag Or dbPageObjectFlag)) = dbPageObjectFlag)
			If (pos And dbModifiedFlag) <> 0 Then
				free(pos And Not dbFlagsMask, Page.pageSize)
			Else
				cloneBitmap(pos And Not dbFlagsMask, Page.pageSize)
			End If
			freeId(oid)
		End Sub

		Protected Overridable Function isDirty() As Boolean
			Return header.dirty
		End Function

		Friend Sub setDirty()
			modified = True
			If header.dirty Then
				Return
			End If
			header.dirty = True
			Dim pg As Page = pool.putPage(0)
			header.pack(pg.data)
			pool.flush()
			pool.unfix(pg)
		End Sub

		Friend Function allocateId() As Integer
			SyncLock objectCache
				Dim oid As Integer
				Dim curr As Integer = 1 - currIndex
				setDirty()
				oid = header.root(curr).freeList
				If oid <> 0 Then
					header.root(curr).freeList = CInt(getPos(oid) >> dbFlagsBits)
					Debug.Assert(header.root(curr).freeList >= 0)
					dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) = dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) Or 1 << ((oid >> dbHandlesPerPageBits) And 31)
					Return oid
				End If

				If currIndexSize >= header.root(curr).indexSize Then
					Dim oldIndexSize As Integer = header.root(curr).indexSize
					Dim newIndexSize As Integer = oldIndexSize * 2
					If newIndexSize < oldIndexSize Then
						newIndexSize = Integer.MaxValue And Not (dbHandlesPerPage - 1)
						If newIndexSize <= oldIndexSize Then
							Throw New DatabaseException(DatabaseException.ErrorCode.NOT_ENOUGH_SPACE)
						End If
					End If
					Dim newIndex As Long = allocate(newIndexSize * 8L, 0)
					If currIndexSize >= header.root(curr).indexSize Then
						Dim oldIndex As Long = header.root(curr).index
						pool.copy(newIndex, oldIndex, currIndexSize * 8L)
						header.root(curr).index = newIndex
						header.root(curr).indexSize = newIndexSize
						free(oldIndex, oldIndexSize * 8L)
					Else
						' index was already reallocated
						free(newIndex, newIndexSize * 8L)
					End If
				End If
				oid = currIndexSize
				header.root(curr).indexUsed = System.Threading.Interlocked.Increment(currIndexSize)
				modified = True
				Return oid
			End SyncLock
		End Function

		Friend Sub freeId(oid As Integer)
			SyncLock objectCache
				setPos(oid, (CLng(header.root(1 - currIndex).freeList) << dbFlagsBits) Or dbFreeHandleFlag)
				header.root(1 - currIndex).freeList = oid
			End SyncLock
		End Sub

		Friend Shared firstHoleSize As Byte() = New Byte() {8, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0, 4, 0, _
			1, 0, 2, 0, 1, 0, _
			3, 0, 1, 0, 2, 0, _
			1, 0, 5, 0, 1, 0, _
			2, 0, 1, 0, 3, 0, _
			1, 0, 2, 0, 1, 0, _
			4, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0, 6, 0, _
			1, 0, 2, 0, 1, 0, _
			3, 0, 1, 0, 2, 0, _
			1, 0, 4, 0, 1, 0, _
			2, 0, 1, 0, 3, 0, _
			1, 0, 2, 0, 1, 0, _
			5, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0, 4, 0, _
			1, 0, 2, 0, 1, 0, _
			3, 0, 1, 0, 2, 0, _
			1, 0, 7, 0, 1, 0, _
			2, 0, 1, 0, 3, 0, _
			1, 0, 2, 0, 1, 0, _
			4, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0, 5, 0, _
			1, 0, 2, 0, 1, 0, _
			3, 0, 1, 0, 2, 0, _
			1, 0, 4, 0, 1, 0, _
			2, 0, 1, 0, 3, 0, _
			1, 0, 2, 0, 1, 0, _
			6, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0, 4, 0, _
			1, 0, 2, 0, 1, 0, _
			3, 0, 1, 0, 2, 0, _
			1, 0, 5, 0, 1, 0, _
			2, 0, 1, 0, 3, 0, _
			1, 0, 2, 0, 1, 0, _
			4, 0, 1, 0, 2, 0, _
			1, 0, 3, 0, 1, 0, _
			2, 0, 1, 0}
		Friend Shared lastHoleSize As Byte() = New Byte() {8, 7, 6, 6, 5, 5, _
			5, 5, 4, 4, 4, 4, _
			4, 4, 4, 4, 3, 3, _
			3, 3, 3, 3, 3, 3, _
			3, 3, 3, 3, 3, 3, _
			3, 3, 2, 2, 2, 2, _
			2, 2, 2, 2, 2, 2, _
			2, 2, 2, 2, 2, 2, _
			2, 2, 2, 2, 2, 2, _
			2, 2, 2, 2, 2, 2, _
			2, 2, 2, 2, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 1, 1, 1, 1, _
			1, 1, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0, 0, 0, _
			0, 0, 0, 0}
		Friend Shared maxHoleSize As Byte() = New Byte() {8, 7, 6, 6, 5, 5, _
			5, 5, 4, 4, 4, 4, _
			4, 4, 4, 4, 4, 3, _
			3, 3, 3, 3, 3, 3, _
			3, 3, 3, 3, 3, 3, _
			3, 3, 5, 4, 3, 3, _
			2, 2, 2, 2, 3, 2, _
			2, 2, 2, 2, 2, 2, _
			4, 3, 2, 2, 2, 2, _
			2, 2, 3, 2, 2, 2, _
			2, 2, 2, 2, 6, 5, _
			4, 4, 3, 3, 3, 3, _
			3, 2, 2, 2, 2, 2, _
			2, 2, 4, 3, 2, 2, _
			2, 1, 1, 1, 3, 2, _
			1, 1, 2, 1, 1, 1, _
			5, 4, 3, 3, 2, 2, _
			2, 2, 3, 2, 1, 1, _
			2, 1, 1, 1, 4, 3, _
			2, 2, 2, 1, 1, 1, _
			3, 2, 1, 1, 2, 1, _
			1, 1, 7, 6, 5, 5, _
			4, 4, 4, 4, 3, 3, _
			3, 3, 3, 3, 3, 3, _
			4, 3, 2, 2, 2, 2, _
			2, 2, 3, 2, 2, 2, _
			2, 2, 2, 2, 5, 4, _
			3, 3, 2, 2, 2, 2, _
			3, 2, 1, 1, 2, 1, _
			1, 1, 4, 3, 2, 2, _
			2, 1, 1, 1, 3, 2, _
			1, 1, 2, 1, 1, 1, _
			6, 5, 4, 4, 3, 3, _
			3, 3, 3, 2, 2, 2, _
			2, 2, 2, 2, 4, 3, _
			2, 2, 2, 1, 1, 1, _
			3, 2, 1, 1, 2, 1, _
			1, 1, 5, 4, 3, 3, _
			2, 2, 2, 2, 3, 2, _
			1, 1, 2, 1, 1, 1, _
			4, 3, 2, 2, 2, 1, _
			1, 1, 3, 2, 1, 1, _
			2, 1, 1, 0}
		Friend Shared maxHoleOffset As Byte() = New Byte() {0, 1, 2, 2, 3, 3, _
			3, 3, 4, 4, 4, 4, _
			4, 4, 4, 4, 0, 1, _
			5, 5, 5, 5, 5, 5, _
			0, 5, 5, 5, 5, 5, _
			5, 5, 0, 1, 2, 2, _
			0, 3, 3, 3, 0, 1, _
			6, 6, 0, 6, 6, 6, _
			0, 1, 2, 2, 0, 6, _
			6, 6, 0, 1, 6, 6, _
			0, 6, 6, 6, 0, 1, _
			2, 2, 3, 3, 3, 3, _
			0, 1, 4, 4, 0, 4, _
			4, 4, 0, 1, 2, 2, _
			0, 1, 0, 3, 0, 1, _
			0, 2, 0, 1, 0, 5, _
			0, 1, 2, 2, 0, 3, _
			3, 3, 0, 1, 0, 2, _
			0, 1, 0, 4, 0, 1, _
			2, 2, 0, 1, 0, 3, _
			0, 1, 0, 2, 0, 1, _
			0, 7, 0, 1, 2, 2, _
			3, 3, 3, 3, 0, 4, _
			4, 4, 4, 4, 4, 4, _
			0, 1, 2, 2, 0, 5, _
			5, 5, 0, 1, 5, 5, _
			0, 5, 5, 5, 0, 1, _
			2, 2, 0, 3, 3, 3, _
			0, 1, 0, 2, 0, 1, _
			0, 4, 0, 1, 2, 2, _
			0, 1, 0, 3, 0, 1, _
			0, 2, 0, 1, 0, 6, _
			0, 1, 2, 2, 3, 3, _
			3, 3, 0, 1, 4, 4, _
			0, 4, 4, 4, 0, 1, _
			2, 2, 0, 1, 0, 3, _
			0, 1, 0, 2, 0, 1, _
			0, 5, 0, 1, 2, 2, _
			0, 3, 3, 3, 0, 1, _
			0, 2, 0, 1, 0, 4, _
			0, 1, 2, 2, 0, 1, _
			0, 3, 0, 1, 0, 2, _
			0, 1, 0, 0}

		Friend Const pageBits As Integer = Page.pageSize * 8
        Friend Const inc As Integer = CInt(Page.pageSize / dbAllocationQuantum) \ 8

		Friend Shared Sub memset(pg As Page, offs As Integer, pattern As Integer, len As Integer)
			Dim arr As Byte() = pg.data
			Dim pat As Byte = CByte(pattern)
			While System.Threading.Interlocked.Decrement(len) >= 0
				arr(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = pat
			End While
		End Sub

		Public ReadOnly Property UsedSize() As Long
			Get
				Return m_usedSize
			End Get
		End Property

		Public ReadOnly Property DatabaseSize() As Long
			Get
				Return header.root(1 - currIndex).size
			End Get
		End Property

		Friend Sub extend(size As Long)
			If size > header.root(1 - currIndex).size Then
				header.root(1 - currIndex).size = size
			End If
		End Sub

		Friend Class Location
			Friend pos As Long
			Friend size As Long
			Friend [next] As Location
		End Class

		Friend Function wasReserved(pos As Long, size As Long) As Boolean
			Dim location As Location = reservedChain
			While location IsNot Nothing
				If (pos >= location.pos AndAlso pos - location.pos < location.size) OrElse (pos <= location.pos AndAlso location.pos - pos < size) Then
					Return True
				End If
				location = location.[next]
			End While
			Return False
		End Function

		Friend Sub reserveLocation(pos As Long, size As Long)
			Dim location As New Location()
			location.pos = pos
			location.size = size
			location.[next] = reservedChain
			reservedChain = location
		End Sub

		Friend Sub commitLocation()
			reservedChain = reservedChain.[next]
		End Sub

		Private Function putBitmapPage(i As Integer) As Page
			Return putPage(getBitmapPageId(i))
		End Function

		Private Function getBitmapPage(i As Integer) As Page
			Return getPage(getBitmapPageId(i))
		End Function

		Friend Function allocate(size As Long, oid As Integer) As Long
			SyncLock objectCache
				setDirty()
				size = (size + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)
				Debug.Assert(size <> 0)
				allocatedDelta += size
				If allocatedDelta > m_gcThreshold Then
					gc0()
				End If

				Dim objBitSize As Integer = CInt(size >> dbAllocationQuantumBits)
				Debug.Assert(objBitSize = (size >> dbAllocationQuantumBits))
				Dim pos As Long
				Dim holeBitSize As Integer = 0
				Dim alignment As Integer = CInt(size) And (Page.pageSize - 1)
				Dim offs As Integer, firstPage As Integer, lastPage As Integer, i As Integer, j As Integer
				Dim holeBeforeFreePage As Integer = 0
				Dim freeBitmapPage As Integer = 0
				Dim curr As Integer = 1 - currIndex
				Dim pg As Page

				lastPage = header.root(curr).bitmapEnd - dbBitmapId
				m_usedSize += size

				If alignment = 0 Then
					firstPage = currPBitmapPage
					offs = (currPBitmapOffs + inc - 1) And Not (inc - 1)
				Else
					firstPage = currRBitmapPage
					offs = currRBitmapOffs
				End If

				While True
					If alignment = 0 Then
						' allocate page object 
						For i = firstPage To lastPage - 1
							Dim spaceNeeded As Integer = If(objBitSize - holeBitSize < pageBits, objBitSize - holeBitSize, pageBits)
							If bitmapPageAvailableSpace(i) <= spaceNeeded Then
								holeBitSize = 0
								offs = 0
								Continue For
							End If
							pg = getBitmapPage(i)
							Dim startOffs As Integer = offs
							While offs < Page.pageSize
								If pg.data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0 Then
									offs = (offs + inc - 1) And Not (inc - 1)
                                    holeBitSize = 0
                                Else
                                    holeBitSize += 8
                                    If holeBitSize = objBitSize Then
                                        pos = ((CLng(i) * Page.pageSize + offs) * 8 - holeBitSize) << dbAllocationQuantumBits
                                        If wasReserved(pos, size) Then
                                            offs += objBitSize >> 3
                                            startOffs = InlineAssignHelper(offs, (offs + inc - 1) And Not (inc - 1))
                                            holeBitSize = 0
                                            Continue While
                                        End If
                                        reserveLocation(pos, size)
                                        currPBitmapPage = i
                                        currPBitmapOffs = offs
                                        extend(pos + size)
                                        If oid <> 0 Then
                                            Dim prev As Long = getPos(oid)
                                            Dim marker As UInteger = CUInt(prev) And dbFlagsMask
                                            pool.copy(pos, prev - marker, size)
                                            setPos(oid, pos Or marker Or dbModifiedFlag)
                                        End If
                                        pool.unfix(pg)
                                        pg = putBitmapPage(i)
                                        Dim holeBytes As Integer = holeBitSize >> 3
                                        If holeBytes > offs Then
                                            memset(pg, 0, &HFF, offs)
                                            holeBytes -= offs
                                            pool.unfix(pg)
                                            pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
                                            offs = Page.pageSize
                                        End If
                                        While holeBytes > Page.pageSize
                                            memset(pg, 0, &HFF, Page.pageSize)
                                            holeBytes -= Page.pageSize
                                            bitmapPageAvailableSpace(i) = 0
                                            pool.unfix(pg)
                                            pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
                                        End While
                                        memset(pg, offs - holeBytes, &HFF, holeBytes)
                                        commitLocation()
                                        pool.unfix(pg)
                                        Return pos
                                    End If
                                End If
							End While
							If startOffs = 0 AndAlso holeBitSize = 0 AndAlso spaceNeeded < bitmapPageAvailableSpace(i) Then
								bitmapPageAvailableSpace(i) = spaceNeeded
							End If
							offs = 0
							pool.unfix(pg)
						Next
					Else
						For i = firstPage To lastPage - 1
							Dim spaceNeeded As Integer = If(objBitSize - holeBitSize < pageBits, objBitSize - holeBitSize, pageBits)
							If bitmapPageAvailableSpace(i) <= spaceNeeded Then
								holeBitSize = 0
								offs = 0
								Continue For
							End If
							pg = getBitmapPage(i)
							Dim startOffs As Integer = offs
							While offs < Page.pageSize
								Dim mask As Integer = pg.data(offs) And &Hff
								If holeBitSize + firstHoleSize(mask) >= objBitSize Then
									pos = ((CLng(i) * Page.pageSize + offs) * 8 - holeBitSize) << dbAllocationQuantumBits
									If wasReserved(pos, size) Then
										startOffs = offs += (objBitSize + 7) >> 3
										holeBitSize = 0
										Continue While
									End If
									reserveLocation(pos, size)
									currRBitmapPage = i
									currRBitmapOffs = offs
									extend(pos + size)
									If oid <> 0 Then
										Dim prev As Long = getPos(oid)
										Dim marker As UInteger = CUInt(prev) And dbFlagsMask
										pool.copy(pos, prev - marker, size)
										setPos(oid, pos Or marker Or dbModifiedFlag)
									End If
									pool.unfix(pg)
									pg = putBitmapPage(i)
									pg.data(offs) = pg.data(offs) Or CByte((1 << (objBitSize - holeBitSize)) - 1)
									If holeBitSize <> 0 Then
										If holeBitSize > offs * 8 Then
											memset(pg, 0, &Hff, offs)
											holeBitSize -= offs * 8
											pool.unfix(pg)
											pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
											offs = Page.pageSize
										End If
										While holeBitSize > pageBits
											memset(pg, 0, &Hff, Page.pageSize)
											holeBitSize -= pageBits
											bitmapPageAvailableSpace(i) = 0
											pool.unfix(pg)
											pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
										End While
										While (holeBitSize -= 8) > 0
											pg.data(System.Threading.Interlocked.Decrement(offs)) = CByte(&Hff)
										End While
										pg.data(offs - 1) = pg.data(offs - 1) Or CByte(Not ((1 << -holeBitSize) - 1))
									End If
									pool.unfix(pg)
									commitLocation()
									Return pos
								ElseIf maxHoleSize(mask) >= objBitSize Then
									Dim holeBitOffset As Integer = maxHoleOffset(mask)
									pos = ((CLng(i) * Page.pageSize + offs) * 8 + holeBitOffset) << dbAllocationQuantumBits
									If wasReserved(pos, size) Then
										startOffs = offs += (objBitSize + 7) >> 3
										holeBitSize = 0
										Continue While
									End If
									reserveLocation(pos, size)
									currRBitmapPage = i
									currRBitmapOffs = offs
									extend(pos + size)
									If oid <> 0 Then
										Dim prev As Long = getPos(oid)
										Dim marker As UInteger = CUInt(prev) And dbFlagsMask
										pool.copy(pos, prev - marker, size)
										setPos(oid, pos Or marker Or dbModifiedFlag)
									End If
									pool.unfix(pg)
									pg = putBitmapPage(i)
									pg.data(offs) = pg.data(offs) Or CByte(((1 << objBitSize) - 1) << holeBitOffset)
									pool.unfix(pg)
									commitLocation()
									Return pos
								End If
								offs += 1
								If lastHoleSize(mask) = 8 Then
									holeBitSize += 8
								Else
									holeBitSize = lastHoleSize(mask)
								End If
							End While
							If startOffs = 0 AndAlso holeBitSize = 0 AndAlso spaceNeeded < bitmapPageAvailableSpace(i) Then
								bitmapPageAvailableSpace(i) = spaceNeeded
							End If
							offs = 0
							pool.unfix(pg)
						Next
					End If
					If firstPage = 0 Then
						If freeBitmapPage > i Then
							i = freeBitmapPage
							holeBitSize = holeBeforeFreePage
						End If
						objBitSize -= holeBitSize
						' number of bits reserved for the object and aligned on page boundary
						Dim skip As Integer = (objBitSize + Page.pageSize / dbAllocationQuantum - 1) And Not (Page.pageSize / dbAllocationQuantum - 1)
						' page aligned position after allocated object
						pos = (CLng(i) << dbBitmapSegmentBits) + (CLng(skip) << dbAllocationQuantumBits)

						Dim extension As Long = If((size > m_extensionQuantum), size, m_extensionQuantum)
						Dim oldIndexSize As Integer = 0
						Dim oldIndex As Long = 0
						Dim morePages As Integer = CInt((extension + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1) \ (Page.pageSize * (dbAllocationQuantum * 8 - 1)))
						If i + morePages > dbLargeBitmapPages Then
							Throw New DatabaseException(DatabaseException.ErrorCode.NOT_ENOUGH_SPACE)
						End If
						If i <= dbBitmapPages AndAlso i + morePages > dbBitmapPages Then
							' We are out of space mapped by memory default allocation bitmap
							oldIndexSize = header.root(curr).indexSize
							If oldIndexSize <= currIndexSize + dbLargeBitmapPages - dbBitmapPages Then
								Dim newIndexSize As Integer = oldIndexSize
								oldIndex = header.root(curr).index
								Do
									newIndexSize <<= 1
									If newIndexSize < 0 Then
										newIndexSize = Integer.MaxValue And Not (dbHandlesPerPage - 1)
										If newIndexSize < currIndexSize + dbLargeBitmapPages - dbBitmapPages Then
											Throw New DatabaseException(DatabaseException.ErrorCode.NOT_ENOUGH_SPACE)
										End If
										Exit Do
									End If
								Loop While newIndexSize <= currIndexSize + dbLargeBitmapPages - dbBitmapPages

								If size + newIndexSize * 8L > m_extensionQuantum Then
									extension = size + newIndexSize * 8L
									morePages = CInt((extension + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1) \ (Page.pageSize * (dbAllocationQuantum * 8 - 1)))
								End If
								extend(pos + CLng(morePages) * Page.pageSize + newIndexSize * 8L)
								Dim newIndex As Long = pos + CLng(morePages) * Page.pageSize
								fillBitmap(pos + (skip >> 3) + CLng(morePages) * (Page.pageSize / dbAllocationQuantum \ 8), newIndexSize >> dbAllocationQuantumBits)
								pool.copy(newIndex, oldIndex, oldIndexSize * 8L)
								header.root(curr).index = newIndex
								header.root(curr).indexSize = newIndexSize
							End If
							Dim newBitmapPageAvailableSpace As Integer() = New Integer(dbLargeBitmapPages - 1) {}
							Array.Copy(bitmapPageAvailableSpace, 0, newBitmapPageAvailableSpace, 0, dbBitmapPages)
							For j = dbBitmapPages To dbLargeBitmapPages - 1
								newBitmapPageAvailableSpace(j) = Integer.MaxValue
							Next
							bitmapPageAvailableSpace = newBitmapPageAvailableSpace

							For j = 0 To dbLargeBitmapPages - dbBitmapPages - 1
								setPos(currIndexSize + j, dbFreeHandleFlag)
							Next

							header.root(curr).bitmapExtent = currIndexSize
							header.root(curr).indexUsed = currIndexSize += dbLargeBitmapPages - dbBitmapPages
						End If
						extend(pos + CLng(morePages) * Page.pageSize)
						Dim adr As Long = pos
						Dim len As Integer = objBitSize >> 3
						' fill bitmap pages used for allocation of object space with 0xFF 
						While len >= Page.pageSize
							pg = pool.putPage(adr)
							memset(pg, 0, &Hff, Page.pageSize)
							pool.unfix(pg)
							adr += Page.pageSize
							len -= Page.pageSize
						End While
						' fill part of last page responsible for allocation of object space
						pg = pool.putPage(adr)
						memset(pg, 0, &Hff, len)
						pg.data(len) = CByte((1 << (objBitSize And 7)) - 1)
						pool.unfix(pg)

						' mark in bitmap newly allocated object
						fillBitmap(pos + (skip >> 3), morePages * (Page.pageSize / dbAllocationQuantum \ 8))

						j = i
						While System.Threading.Interlocked.Decrement(morePages) >= 0
							setPos(getBitmapPageId(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)), pos Or dbPageObjectFlag Or dbModifiedFlag)
							pos += Page.pageSize
						End While
						header.root(curr).bitmapEnd = j + dbBitmapId
						j = i + objBitSize \ pageBits
						If alignment <> 0 Then
							currRBitmapPage = j
							currRBitmapOffs = 0
						Else
							currPBitmapPage = j
							currPBitmapOffs = 0
						End If
						While j > i
							bitmapPageAvailableSpace(System.Threading.Interlocked.Decrement(j)) = 0
						End While

						pos = (CLng(i) * Page.pageSize * 8 - holeBitSize) << dbAllocationQuantumBits
						If oid <> 0 Then
							Dim prev As Long = getPos(oid)
							Dim marker As UInteger = CUInt(prev) And dbFlagsMask
							pool.copy(pos, prev - marker, size)
							setPos(oid, pos Or marker Or dbModifiedFlag)
						End If

						If holeBitSize <> 0 Then
							reserveLocation(pos, size)
							While holeBitSize > pageBits
								holeBitSize -= pageBits
								pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
								memset(pg, 0, &Hff, Page.pageSize)
								bitmapPageAvailableSpace(i) = 0
								pool.unfix(pg)
							End While
							pg = putBitmapPage(System.Threading.Interlocked.Decrement(i))
							offs = Page.pageSize
							While (holeBitSize -= 8) > 0
								pg.data(System.Threading.Interlocked.Decrement(offs)) = CByte(&Hff)
							End While
							pg.data(offs - 1) = pg.data(offs - 1) Or CByte(Not ((1 << -holeBitSize) - 1))
							pool.unfix(pg)
							commitLocation()
						End If
						If oldIndex <> 0 Then
							free(oldIndex, oldIndexSize * 8L)
						End If

						Return pos
					End If
					If m_gcThreshold <> Int64.MaxValue AndAlso Not gcDone Then
						allocatedDelta -= size
						m_usedSize -= size
						gc0()
						currRBitmapPage = InlineAssignHelper(currPBitmapPage, 0)
						currRBitmapOffs = InlineAssignHelper(currPBitmapOffs, 0)
						Return allocate(size, oid)
					End If
					freeBitmapPage = i
					holeBeforeFreePage = holeBitSize
					holeBitSize = 0
					lastPage = firstPage + 1
					firstPage = 0
					offs = 0
				End While
			End SyncLock
		End Function

		Private Sub fillBitmap(adr As Long, len As Integer)
			While True
				Dim off As Integer = CInt(adr) And (Page.pageSize - 1)
				Dim pg As Page = pool.putPage(adr - off)
				If Page.pageSize - off >= len Then
					memset(pg, off, &Hff, len)
					pool.unfix(pg)
					Exit While
				Else
					memset(pg, off, &Hff, Page.pageSize - off)
					pool.unfix(pg)
					adr += Page.pageSize - off
					len -= Page.pageSize - off
				End If
			End While
		End Sub

		Friend Sub free(pos As Long, size As Long)
			SyncLock objectCache
				Debug.Assert(pos <> 0 AndAlso (pos And (dbAllocationQuantum - 1)) = 0)
				Dim quantNo As Long = pos >> dbAllocationQuantumBits
				Dim objBitSize As Integer = CInt((size + dbAllocationQuantum - 1) >> dbAllocationQuantumBits)
				Dim pageId As Integer = CInt(quantNo >> (Page.pageBits + 3))
				Dim offs As Integer = CInt(quantNo And (Page.pageSize * 8 - 1)) >> 3
				Dim pg As Page = putBitmapPage(pageId)
				Dim bitOffs As Integer = CInt(quantNo) And 7

				allocatedDelta -= CLng(objBitSize) << dbAllocationQuantumBits
				m_usedSize -= CLng(objBitSize) << dbAllocationQuantumBits

				If (pos And (Page.pageSize - 1)) = 0 AndAlso size >= Page.pageSize Then
					If pageId = currPBitmapPage AndAlso offs < currPBitmapOffs Then
						currPBitmapOffs = offs
					End If
				End If
				If pageId = currRBitmapPage AndAlso offs < currRBitmapOffs Then
					currRBitmapOffs = offs
				End If
				bitmapPageAvailableSpace(pageId) = System.Int32.MaxValue

				If objBitSize > 8 - bitOffs Then
					objBitSize -= 8 - bitOffs
					pg.data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = pg.data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) And CByte((1 << bitOffs) - 1)
					While objBitSize + offs * 8 > Page.pageSize * 8
						memset(pg, offs, 0, Page.pageSize - offs)
						pool.unfix(pg)
						pg = putBitmapPage(System.Threading.Interlocked.Increment(pageId))
						bitmapPageAvailableSpace(pageId) = System.Int32.MaxValue
						objBitSize -= (Page.pageSize - offs) * 8
						offs = 0
					End While
					While (objBitSize -= 8) > 0
						pg.data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(0)
					End While
					pg.data(offs) = pg.data(offs) And CByte(Not ((1 << (objBitSize + 8)) - 1))
				Else
					pg.data(offs) = pg.data(offs) And CByte(Not (((1 << objBitSize) - 1) << bitOffs))
				End If
				pool.unfix(pg)
			End SyncLock
		End Sub

		Friend Sub cloneBitmap(pos As Long, size As Long)
			SyncLock objectCache
				Dim quantNo As Long = pos >> dbAllocationQuantumBits
				Dim objBitSize As Integer = CInt((size + dbAllocationQuantum - 1) >> dbAllocationQuantumBits)
				Dim pageId As Integer = CInt(quantNo >> (Page.pageBits + 3))
				Dim offs As Integer = CInt(quantNo And (Page.pageSize * 8 - 1)) >> 3
				Dim bitOffs As Integer = CInt(quantNo) And 7
				Dim oid As Integer = getBitmapPageId(pageId)
				pos = getPos(oid)
				If (pos And dbModifiedFlag) = 0 Then
					dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) = dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) Or 1 << ((oid >> dbHandlesPerPageBits) And 31)
					allocate(Page.pageSize, oid)
					cloneBitmap(pos And Not dbFlagsMask, Page.pageSize)
				End If

				If objBitSize > 8 - bitOffs Then
					objBitSize -= 8 - bitOffs
					offs += 1
					While objBitSize + offs * 8 > Page.pageSize * 8
						oid = getBitmapPageId(System.Threading.Interlocked.Increment(pageId))
						pos = getPos(oid)
						If (pos And dbModifiedFlag) = 0 Then
							dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) = dirtyPagesMap(oid >> (dbHandlesPerPageBits + 5)) Or 1 << ((oid >> dbHandlesPerPageBits) And 31)
							allocate(Page.pageSize, oid)
							cloneBitmap(pos And Not dbFlagsMask, Page.pageSize)
						End If
						objBitSize -= (Page.pageSize - offs) * 8
						offs = 0
					End While
				End If
			End SyncLock
		End Sub

        Public Sub Open(filePath As [String]) Implements IDatabase.Open
            Open(filePath, DEFAULT_PAGE_POOL_SIZE)
        End Sub

        Public Sub Open(file As IFile) Implements IDatabase.Open
            Open(file, DEFAULT_PAGE_POOL_SIZE)
        End Sub

        Public Sub Open(filePath As [String], cacheSizeInBytes As Integer) Implements IDatabase.Open
            Dim file As New OsFile(filePath)
            Try
                Open(file, cacheSizeInBytes)
            Catch generatedExceptionName As DatabaseException
                file.Close()
                Throw
            End Try
        End Sub

		Protected Overridable Function createObjectCache(kind As CacheType, cacheSizeInBytes As Integer, objectCacheSize As Integer) As OidHashTable
			If cacheSizeInBytes = 0 OrElse kind = CacheType.Strong Then
				Return New StrongHashTable(objectCacheSize)
			End If

			If kind = CacheType.Weak Then
				Return New WeakHashTable(objectCacheSize)
			End If

			Debug.Assert(kind = CacheType.Lru)
			Return New LruObjectCache(objectCacheSize)
		End Function

        Public Overridable Sub Open(file As IFile, cacheSizeInBytes As Integer) Implements IDatabase.Open
            SyncLock Me
                If opened Then
                    Throw New DatabaseException(DatabaseException.ErrorCode.DATABASE_ALREADY_OPENED)
                End If

                file.Lock()
                Dim pg As Page
                Dim i As Integer
                Dim indexSize As Integer = initIndexSize
                If indexSize < dbFirstUserId Then
                    indexSize = dbFirstUserId
                End If

                indexSize = (indexSize + dbHandlesPerPage - 1) And Not (dbHandlesPerPage - 1)

                dirtyPagesMap = New Integer(dbDirtyPageBitmapSize \ 4) {}
                m_gcThreshold = Int64.MaxValue
                backgroundGcMonitor = New Object()
                backgroundGcStartMonitor = New Object()
                gcThread = Nothing
                gcGo = False
                gcActive = False
                gcDone = False
                allocatedDelta = 0

                resolvedTypes = New Dictionary(Of String, Type)()

                nNestedTransactions = 0
                nBlockedTransactions = 0
                nCommittedTransactions = 0
                scheduledCommitTime = Int64.MaxValue
#If CF Then
				transactionMonitor = New CNetMonitor()
#Else
                transactionMonitor = New Object()
#End If
                transactionLock = New PersistentResource()

                modified = False

                objectCache = createObjectCache(m_cacheKind, cacheSizeInBytes, m_objectCacheInitSize)

                classDescMap = New Dictionary(Of Type, ClassDescriptor)()
                descList = Nothing

                objectFormatter = New System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()

                header = New Header()
                Dim buf As Byte() = New Byte(header.Sizeof - 1) {}
                Dim rc As Integer = file.Read(0, buf)
                If rc > 0 AndAlso rc < header.Sizeof Then
                    Throw New DatabaseException(DatabaseException.ErrorCode.DATABASE_CORRUPTED)
                End If

                header.unpack(buf)
                If header.curr < 0 OrElse header.curr > 1 Then
                    Throw New DatabaseException(DatabaseException.ErrorCode.DATABASE_CORRUPTED)
                End If

                If pool Is Nothing Then
                    pool = New PagePool(cacheSizeInBytes / Page.pageSize)
                    pool.open(file)
                End If

                If Not header.initialized Then
                    header.curr = InlineAssignHelper(currIndex, 0)
                    Dim used As Long = Page.pageSize
                    header.root(0).index = used
                    header.root(0).indexSize = indexSize
                    header.root(0).indexUsed = dbFirstUserId
                    header.root(0).freeList = 0
                    used += indexSize * 8L
                    header.root(1).index = used
                    header.root(1).indexSize = indexSize
                    header.root(1).indexUsed = dbFirstUserId
                    header.root(1).freeList = 0
                    used += indexSize * 8L

                    header.root(0).shadowIndex = header.root(1).index
                    header.root(1).shadowIndex = header.root(0).index
                    header.root(0).shadowIndexSize = indexSize
                    header.root(1).shadowIndexSize = indexSize

                    Dim bitmapPages As Integer = CInt((used + Page.pageSize * (dbAllocationQuantum * 8 - 1) - 1) \ (Page.pageSize * (dbAllocationQuantum * 8 - 1)))
                    Dim bitmapSize As Long = CLng(bitmapPages) * Page.pageSize
                    Dim usedBitmapSize As Integer = CInt((used + bitmapSize) >> (dbAllocationQuantumBits + 3))

                    For i = 0 To bitmapPages - 1
                        pg = pool.putPage(used + CLng(i) * Page.pageSize)
                        Dim bitmap As Byte() = pg.data
                        Dim n As Integer = If(usedBitmapSize > Page.pageSize, Page.pageSize, usedBitmapSize)
                        For j As Integer = 0 To n - 1
                            bitmap(j) = CByte(&HFF)
                        Next
                        pool.unfix(pg)
                    Next

                    Dim bitmapIndexSize As Integer = ((dbBitmapId + dbBitmapPages) * 8 + Page.pageSize - 1) And Not (Page.pageSize - 1)
                    Dim index As Byte() = New Byte(bitmapIndexSize - 1) {}
                    Bytes.pack8(index, dbInvalidId * 8, dbFreeHandleFlag)
                    For i = 0 To bitmapPages - 1
                        Bytes.pack8(index, (dbBitmapId + i) * 8, used Or dbPageObjectFlag)
                        used += Page.pageSize
                    Next
                    header.root(0).bitmapEnd = dbBitmapId + i
                    header.root(1).bitmapEnd = dbBitmapId + i
                    While i < dbBitmapPages
                        Bytes.pack8(index, (dbBitmapId + i) * 8, dbFreeHandleFlag)
                        i += 1
                    End While
                    header.root(0).size = used
                    header.root(1).size = used
                    m_usedSize = used
                    committedIndexSize = InlineAssignHelper(currIndexSize, dbFirstUserId)

                    pool.write(header.root(1).index, index)
                    pool.write(header.root(0).index, index)

                    header.dirty = True
                    header.root(0).size = header.root(1).size
                    pg = pool.putPage(0)
                    header.pack(pg.data)
                    pool.flush()
                    pool.modify(pg)
                    header.initialized = True
                    header.pack(pg.data)
                    pool.unfix(pg)
                    pool.flush()
                Else
                    Dim curr As Integer = header.curr
                    currIndex = curr
                    If header.root(curr).indexSize <> header.root(curr).shadowIndexSize Then
                        Throw New DatabaseException(DatabaseException.ErrorCode.DATABASE_CORRUPTED)
                    End If

                    If isDirty() Then
                        If Listener IsNot Nothing Then
                            Listener.DatabaseCorrupted()
                        End If

                        'System.Console.WriteLine("Database was not normally closed: start recovery");
                        header.root(1 - curr).size = header.root(curr).size
                        header.root(1 - curr).indexUsed = header.root(curr).indexUsed
                        header.root(1 - curr).freeList = header.root(curr).freeList
                        header.root(1 - curr).index = header.root(curr).shadowIndex
                        header.root(1 - curr).indexSize = header.root(curr).shadowIndexSize
                        header.root(1 - curr).shadowIndex = header.root(curr).index
                        header.root(1 - curr).shadowIndexSize = header.root(curr).indexSize
                        header.root(1 - curr).bitmapEnd = header.root(curr).bitmapEnd
                        header.root(1 - curr).rootObject = header.root(curr).rootObject
                        header.root(1 - curr).classDescList = header.root(curr).classDescList
                        header.root(1 - curr).bitmapExtent = header.root(curr).bitmapExtent

                        pg = pool.putPage(0)
                        header.pack(pg.data)
                        pool.unfix(pg)

                        pool.copy(header.root(1 - curr).index, header.root(curr).index, (header.root(curr).indexUsed * 8L + Page.pageSize - 1) And Not (Page.pageSize - 1))
                        If Listener IsNot Nothing Then
                            Listener.RecoveryCompleted()

                        End If
                    End If
                    currIndexSize = header.root(1 - curr).indexUsed
                    committedIndexSize = currIndexSize
                    m_usedSize = header.root(curr).size
                End If
                Dim nBitmapPages As Integer = If(header.root(1 - currIndex).bitmapExtent = 0, dbBitmapPages, dbLargeBitmapPages)
                bitmapPageAvailableSpace = New Integer(nBitmapPages - 1) {}
                For i = 0 To bitmapPageAvailableSpace.Length - 1
                    bitmapPageAvailableSpace(i) = Integer.MaxValue
                Next
                currRBitmapPage = InlineAssignHelper(currPBitmapPage, 0)
                currRBitmapOffs = InlineAssignHelper(currPBitmapOffs, 0)

                opened = True
                reloadScheme()
            End SyncLock
        End Sub

		Public ReadOnly Property IsOpened() As Boolean
			Get
				Return opened
			End Get
		End Property

		Friend Shared Sub checkIfFinal(desc As ClassDescriptor)
			Dim cls As System.Type = desc.cls
			Dim [next] As ClassDescriptor = desc.[next]
			While [next] IsNot Nothing
				[next].Load()
				If cls.IsAssignableFrom([next].cls) Then
					desc.hasSubclasses = True
				ElseIf [next].cls.IsAssignableFrom(cls) Then
					[next].hasSubclasses = True
				End If
				[next] = [next].[next]
			End While
		End Sub

		Friend Sub reloadScheme()
			classDescMap.Clear()
			Dim descListOid As Integer = header.root(1 - currIndex).classDescList
			classDescMap(GetType(ClassDescriptor)) = New ClassDescriptor(Me, GetType(ClassDescriptor))
			classDescMap(GetType(ClassDescriptor.FieldDescriptor)) = New ClassDescriptor(Me, GetType(ClassDescriptor.FieldDescriptor))
			If descListOid <> 0 Then
				Dim desc As ClassDescriptor
				descList = findClassDescriptor(descListOid)
				desc = descList
				While desc IsNot Nothing
					desc.Load()
					desc = desc.[next]
				End While
				desc = descList
				While desc IsNot Nothing
					If classDescMap(desc.cls) = desc Then
						desc.resolve()
					End If

					checkIfFinal(desc)
					desc = desc.[next]
				End While
			Else
				descList = Nothing
			End If
			#If Not CF Then
			If enableCodeGeneration Then
				codeGenerationThread = New Thread(New ThreadStart(AddressOf generateSerializers))
				codeGenerationThread.Priority = ThreadPriority.BelowNormal
				codeGenerationThread.IsBackground = True
				codeGenerationThread.Start()
			End If
			#End If
		End Sub

		Friend Sub generateSerializers()
			Dim desc As ClassDescriptor = descList
			While desc IsNot Nothing
				desc.generateSerializer()
				desc = desc.[next]
			End While
		End Sub

		Friend Sub assignOid(obj As IPersistent, oid As Integer)
			obj.AssignOid(Me, oid, False)
		End Sub

		Friend Sub registerClassDescriptor(desc As ClassDescriptor)
			classDescMap(desc.cls) = desc
			desc.[next] = descList
			descList = desc
			checkIfFinal(desc)
			storeObject0(desc)
			header.root(1 - currIndex).classDescList = desc.Oid
			modified = True
		End Sub

		Friend Function getClassDescriptor(cls As System.Type) As ClassDescriptor
			Dim desc As ClassDescriptor
			Dim found = classDescMap.TryGetValue(cls, desc)
			If Not found Then
				desc = New ClassDescriptor(Me, cls)
				desc.generateSerializer()
				registerClassDescriptor(desc)
			End If
			Return desc
		End Function

        Public Sub Commit() Implements IDatabase.Commit
            SyncLock backgroundGcMonitor
                SyncLock Me
                    ensureOpened()
                    objectCache.Flush()

                    If Not modified Then
                        Return
                    End If

                    commit0()
                    modified = False
                End SyncLock
            End SyncLock
        End Sub

		Private Sub commit0()
			Dim curr As Integer = currIndex
			Dim i As Integer, j As Integer, n As Integer
			Dim map As Integer() = dirtyPagesMap
			Dim oldIndexSize As Integer = header.root(curr).indexSize
			Dim newIndexSize As Integer = header.root(1 - curr).indexSize
			Dim nPages As Integer = committedIndexSize >> dbHandlesPerPageBits
			Dim pg As Page

			If newIndexSize > oldIndexSize Then
				cloneBitmap(header.root(curr).index, oldIndexSize * 8L)
				Dim newIndex As Long
				While True
					newIndex = allocate(newIndexSize * 8L, 0)
					If newIndexSize = header.root(1 - curr).indexSize Then
						Exit While
					End If
					free(newIndex, newIndexSize * 8L)
					newIndexSize = header.root(1 - curr).indexSize
				End While
				header.root(1 - curr).shadowIndex = newIndex
				header.root(1 - curr).shadowIndexSize = newIndexSize
				free(header.root(curr).index, oldIndexSize * 8L)
			End If

			For i = 0 To nPages - 1
				If (map(i >> 5) And (1 << (i And 31))) <> 0 Then
					Dim srcIndex As Page = pool.getPage(header.root(1 - curr).index + CLng(i) * Page.pageSize)
					Dim dstIndex As Page = pool.getPage(header.root(curr).index + CLng(i) * Page.pageSize)
					For j = 0 To Page.pageSize - 1 Step 8
						Dim pos As Long = Bytes.unpack8(dstIndex.data, j)
						If Bytes.unpack8(srcIndex.data, j) <> pos Then
							If (pos And dbFreeHandleFlag) = 0 Then
								If (pos And dbPageObjectFlag) <> 0 Then
									free(pos And Not dbFlagsMask, Page.pageSize)
								ElseIf pos <> 0 Then
									Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
									pg = pool.getPage(pos - offs)
									free(pos, ObjectHeader.getSize(pg.data, offs))
									pool.unfix(pg)
								End If
							End If
						End If
					Next
					pool.unfix(srcIndex)
					pool.unfix(dstIndex)
				End If
			Next
			n = committedIndexSize And (dbHandlesPerPage - 1)
			If n <> 0 AndAlso (map(i >> 5) And (1 << (i And 31))) <> 0 Then
				Dim srcIndex As Page = pool.getPage(header.root(1 - curr).index + CLng(i) * Page.pageSize)
				Dim dstIndex As Page = pool.getPage(header.root(curr).index + CLng(i) * Page.pageSize)
				j = 0
				Do
					Dim pos As Long = Bytes.unpack8(dstIndex.data, j)
					If Bytes.unpack8(srcIndex.data, j) <> pos Then
						If (pos And dbFreeHandleFlag) = 0 Then
							If (pos And dbPageObjectFlag) <> 0 Then
								free(pos And Not dbFlagsMask, Page.pageSize)
							ElseIf pos <> 0 Then
								Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
								pg = pool.getPage(pos - offs)
								free(pos, ObjectHeader.getSize(pg.data, offs))
								pool.unfix(pg)
							End If
						End If
					End If
					j += 8
				Loop While System.Threading.Interlocked.Decrement(n) <> 0

				pool.unfix(srcIndex)
				pool.unfix(dstIndex)
			End If

			For i = 0 To nPages
				If (map(i >> 5) And (1 << (i And 31))) <> 0 Then
					pg = pool.putPage(header.root(1 - curr).index + CLng(i) * Page.pageSize)
					For j = 0 To Page.pageSize - 1 Step 8
						Bytes.pack8(pg.data, j, Bytes.unpack8(pg.data, j) And Not dbModifiedFlag)
					Next
					pool.unfix(pg)
				End If
			Next

			If currIndexSize > committedIndexSize Then
				Dim page__1 As Long = (header.root(1 - curr).index + committedIndexSize * 8L) And Not (Page.pageSize - 1)
				Dim [end] As Long = (header.root(1 - curr).index + Page.pageSize - 1 + currIndexSize * 8L) And Not (Page.pageSize - 1)
				While page__1 < [end]
					pg = pool.putPage(page__1)
					For j = 0 To Page.pageSize - 1 Step 8
						Bytes.pack8(pg.data, j, Bytes.unpack8(pg.data, j) And Not dbModifiedFlag)
					Next
					pool.unfix(pg)
					page__1 += Page.pageSize
				End While
			End If
			header.root(1 - curr).usedSize = m_usedSize
			pg = pool.putPage(0)
			header.pack(pg.data)
			pool.flush()
			pool.modify(pg)
			header.curr = curr = curr Xor 1
			header.dirty = True
			header.pack(pg.data)
			pool.unfix(pg)
			pool.flush()

			header.root(1 - curr).size = header.root(curr).size
			header.root(1 - curr).indexUsed = currIndexSize
			header.root(1 - curr).freeList = header.root(curr).freeList
			header.root(1 - curr).bitmapEnd = header.root(curr).bitmapEnd
			header.root(1 - curr).rootObject = header.root(curr).rootObject
			header.root(1 - curr).classDescList = header.root(curr).classDescList
			header.root(1 - curr).bitmapExtent = header.root(curr).bitmapExtent

			If currIndexSize = 0 OrElse newIndexSize <> oldIndexSize Then
				If currIndexSize = 0 Then
					currIndexSize = header.root(1 - curr).indexUsed
				End If
				header.root(1 - curr).index = header.root(curr).shadowIndex
				header.root(1 - curr).indexSize = header.root(curr).shadowIndexSize
				header.root(1 - curr).shadowIndex = header.root(curr).index
				header.root(1 - curr).shadowIndexSize = header.root(curr).indexSize
				pool.copy(header.root(1 - curr).index, header.root(curr).index, currIndexSize * 8L)
				i = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5)
				While System.Threading.Interlocked.Decrement(i) >= 0
					map(i) = 0
				End While
			Else
				For i = 0 To nPages - 1
					If (map(i >> 5) And (1 << (i And 31))) <> 0 Then
						map(i >> 5) -= (1 << (i And 31))
						pool.copy(header.root(1 - curr).index + CLng(i) * Page.pageSize, header.root(curr).index + CLng(i) * Page.pageSize, Page.pageSize)
					End If
				Next
				If currIndexSize > i * dbHandlesPerPage AndAlso ((map(i >> 5) And (1 << (i And 31))) <> 0 OrElse currIndexSize <> committedIndexSize) Then
					pool.copy(header.root(1 - curr).index + CLng(i) * Page.pageSize, header.root(curr).index + CLng(i) * Page.pageSize, 8L * currIndexSize - CLng(i) * Page.pageSize)
					j = i >> 5
					n = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5)
					While j < n
						map(System.Math.Max(System.Threading.Interlocked.Increment(j),j - 1)) = 0
					End While
				End If
			End If
			gcDone = False
			currIndex = curr
			committedIndexSize = currIndexSize
		End Sub

        Public Sub Rollback() Implements IDatabase.Rollback
            SyncLock Me
                ensureOpened()
                objectCache.Invalidate()

                If Not modified Then
                    Return
                End If

                rollback0()
                modified = False
            End SyncLock
        End Sub

		Private Sub rollback0()
			Dim curr As Integer = currIndex
			Dim map As Integer() = dirtyPagesMap
			If header.root(1 - curr).index <> header.root(curr).shadowIndex Then
				pool.copy(header.root(curr).shadowIndex, header.root(curr).index, 8L * committedIndexSize)
			Else
				Dim nPages As Integer = (committedIndexSize + dbHandlesPerPage - 1) >> dbHandlesPerPageBits
				For i As Integer = 0 To nPages - 1
					If (map(i >> 5) And (1 << (i And 31))) <> 0 Then
						pool.copy(header.root(curr).shadowIndex + CLng(i) * Page.pageSize, header.root(curr).index + CLng(i) * Page.pageSize, Page.pageSize)
					End If
				Next
			End If
			Dim j As Integer = (currIndexSize + dbHandlesPerPage * 32 - 1) >> (dbHandlesPerPageBits + 5)
			While System.Threading.Interlocked.Decrement(j) >= 0
				

				map(j) = 0
			End While
			header.root(1 - curr).index = header.root(curr).shadowIndex
			header.root(1 - curr).indexSize = header.root(curr).shadowIndexSize
			header.root(1 - curr).indexUsed = committedIndexSize
			header.root(1 - curr).freeList = header.root(curr).freeList
			header.root(1 - curr).bitmapEnd = header.root(curr).bitmapEnd
			header.root(1 - curr).size = header.root(curr).size
			header.root(1 - curr).rootObject = header.root(curr).rootObject
			header.root(1 - curr).classDescList = header.root(curr).classDescList
			header.root(1 - curr).bitmapExtent = header.root(curr).bitmapExtent
			header.dirty = True
			m_usedSize = header.root(curr).size
			currIndexSize = committedIndexSize

			currRBitmapPage = InlineAssignHelper(currPBitmapPage, 0)
			currRBitmapOffs = InlineAssignHelper(currPBitmapOffs, 0)

			reloadScheme()
		End Sub

		Private Sub memset(arr As Byte(), off As Integer, len As Integer, val As Byte)
			While System.Threading.Interlocked.Decrement(len) >= 0
				arr(System.Math.Max(System.Threading.Interlocked.Increment(off),off - 1)) = val
			End While
		End Sub

		#If CF Then
		Private Class PositionComparer
			Implements System.Collections.IComparer
			Public Function Compare(o1 As Object, o2 As Object) As Integer Implements IComparer.Compare
				Dim i1 As Long = CLng(o1)
				Dim i2 As Long = CLng(o2)
				Return If(i1 < i2, -1, If(i1 = i2, 0, 1))
			End Function
		End Class
		#Else
        Public Function CreateClass(type As Type) As IPersistent Implements IDatabase.CreateClass
            SyncLock Me
                ensureOpened()

                SyncLock objectCache
                    Dim wrapper As Type = getWrapper(type)
                    Dim obj As IPersistent = DirectCast(wrapper.Assembly.CreateInstance(wrapper.Name), IPersistent)
                    Dim oid As Integer = allocateId()
                    obj.AssignOid(Me, oid, False)
                    setPos(oid, 0)
                    objectCache.Put(oid, obj)
                    obj.Modify()
                    Return obj
                End SyncLock
            End SyncLock
        End Function

		Friend Function getWrapper(original As Type) As Type
			Dim wrapper As Type
			Dim ok As Boolean = wrapperHash.TryGetValue(original, wrapper)
			If Not ok Then
				wrapper = CodeGenerator.Instance.CreateWrapper(original)
				wrapperHash(original) = wrapper
			End If
			Return wrapper
		End Function
		#End If
		Public Function MakePersistent(obj As IPersistent) As Integer
			If obj Is Nothing Then
				Return 0
			End If
			If obj.Oid <> 0 Then
				Return obj.Oid
			End If

			SyncLock Me
				ensureOpened()
				SyncLock objectCache
					Dim oid As Integer = allocateId()
					obj.AssignOid(Me, oid, False)
					setPos(oid, 0)
					objectCache.Put(oid, obj)
					obj.Modify()
					Return oid
				End SyncLock
			End SyncLock
		End Function

        Public Sub Backup(stream As System.IO.Stream) Implements IDatabase.Backup
            SyncLock Me
                ensureOpened()
                objectCache.Flush()
                Dim curr As Integer = 1 - currIndex
                Dim nObjects As Integer = header.root(curr).indexUsed
                Dim indexOffs As Long = header.root(curr).index
                Dim i As Integer, j As Integer, k As Integer
                Dim nUsedIndexPages As Integer = (nObjects + dbHandlesPerPage - 1) \ dbHandlesPerPage
                Dim nIndexPages As Integer = CInt((header.root(curr).indexSize + dbHandlesPerPage - 1) \ dbHandlesPerPage)
                Dim totalRecordsSize As Long = 0
                Dim nPagedObjects As Long = 0
                Dim bitmapExtent As Integer = header.root(curr).bitmapExtent
                Dim index As Long() = New Long(nObjects - 1) {}
                Dim oids As Integer() = New Integer(nObjects - 1) {}

                If bitmapExtent = 0 Then
                    bitmapExtent = Integer.MaxValue
                End If

                i = 0
                j = 0
                While i < nUsedIndexPages
                    Dim pg As Page = pool.getPage(indexOffs + CLng(i) * Page.pageSize)
                    k = 0
                    While k < dbHandlesPerPage AndAlso j < nObjects
                        Dim pos As Long = Bytes.unpack8(pg.data, k * 8)
                        index(j) = pos
                        oids(j) = j
                        If (pos And dbFreeHandleFlag) = 0 Then
                            If (pos And dbPageObjectFlag) <> 0 Then
                                nPagedObjects += 1
                            ElseIf pos <> 0 Then
                                Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
                                Dim op As Page = pool.getPage(pos - offs)
                                Dim size As Integer = ObjectHeader.getSize(op.data, offs And Not dbFlagsMask)
                                size = (size + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)
                                totalRecordsSize += size
                                pool.unfix(op)
                            End If
                        End If
                        k += 1
                        j += 1
                    End While

                    pool.unfix(pg)
                    i += 1
                End While
                Dim newHeader As New Header()
                newHeader.curr = 0
                newHeader.dirty = False
                newHeader.initialized = True
                Dim newFileSize As Long = CLng(nPagedObjects + nIndexPages * 2 + 1) * Page.pageSize + totalRecordsSize
                newFileSize = (newFileSize + Page.pageSize - 1) And Not (Page.pageSize - 1)
                newHeader.root = New RootPage(1) {}
                newHeader.root(0) = New RootPage()
                newHeader.root(1) = New RootPage()
                newHeader.root(0).size = InlineAssignHelper(newHeader.root(1).size, newFileSize)
                newHeader.root(0).index = InlineAssignHelper(newHeader.root(1).shadowIndex, Page.pageSize)
                newHeader.root(0).shadowIndex = InlineAssignHelper(newHeader.root(1).index, Page.pageSize + CLng(nIndexPages) * Page.pageSize)
                newHeader.root(0).shadowIndexSize = InlineAssignHelper(newHeader.root(0).indexSize, InlineAssignHelper(newHeader.root(1).shadowIndexSize, InlineAssignHelper(newHeader.root(1).indexSize, nIndexPages * dbHandlesPerPage)))
                newHeader.root(0).indexUsed = InlineAssignHelper(newHeader.root(1).indexUsed, nObjects)
                newHeader.root(0).freeList = InlineAssignHelper(newHeader.root(1).freeList, header.root(curr).freeList)
                newHeader.root(0).bitmapEnd = InlineAssignHelper(newHeader.root(1).bitmapEnd, header.root(curr).bitmapEnd)

                newHeader.root(0).rootObject = InlineAssignHelper(newHeader.root(1).rootObject, header.root(curr).rootObject)
                newHeader.root(0).classDescList = InlineAssignHelper(newHeader.root(1).classDescList, header.root(curr).classDescList)
                newHeader.root(0).bitmapExtent = InlineAssignHelper(newHeader.root(1).bitmapExtent, bitmapExtent)

                Dim page__1 As Byte() = New Byte(Page.pageSize - 1) {}
                newHeader.pack(page__1)
                stream.Write(page__1, 0, Page.pageSize)

                Dim pageOffs As Long = CLng(nIndexPages * 2 + 1) * Page.pageSize
                Dim recOffs As Long = CLng(nPagedObjects + nIndexPages * 2 + 1) * Page.pageSize
#If CF Then
				Array.Sort(index, oids, 0, nObjects, New PositionComparer())
#Else
                Array.Sort(index, oids)
#End If
                Dim newIndex As Byte() = New Byte(nIndexPages * dbHandlesPerPage * 8 - 1) {}
                For i = 0 To nObjects - 1
                    Dim pos As Long = index(i)
                    Dim oid As Integer = oids(i)
                    If pos <> 0 AndAlso (pos And dbFreeHandleFlag) = 0 Then
                        If (pos And dbPageObjectFlag) <> 0 Then
                            Bytes.pack8(newIndex, oid * 8, pageOffs Or dbPageObjectFlag)
                            pageOffs += Page.pageSize
                        Else
                            Bytes.pack8(newIndex, oid * 8, recOffs)
                            Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
                            Dim op As Page = pool.getPage(pos - offs)
                            Dim size As Integer = ObjectHeader.getSize(op.data, offs And Not dbFlagsMask)
                            size = (size + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)
                            recOffs += size
                            pool.unfix(op)
                        End If
                    Else
                        Bytes.pack8(newIndex, oid * 8, pos)
                    End If
                Next
                stream.Write(newIndex, 0, newIndex.Length)
                stream.Write(newIndex, 0, newIndex.Length)

                For i = 0 To nObjects - 1
                    Dim pos As Long = index(i)
                    If (CInt(pos) And (dbFreeHandleFlag Or dbPageObjectFlag)) = dbPageObjectFlag Then
                        If oids(i) < dbBitmapId + dbBitmapPages OrElse (oids(i) >= bitmapExtent AndAlso oids(i) < bitmapExtent + dbLargeBitmapPages - dbBitmapPages) Then
                            Dim pageId As Integer = If(oids(i) < dbBitmapId + dbBitmapPages, oids(i) - dbBitmapId, oids(i) - bitmapExtent)
                            Dim mappedSpace As Long = CLng(pageId) * Page.pageSize * 8 * dbAllocationQuantum
                            If mappedSpace >= newFileSize Then
                                memset(page__1, 0, Page.pageSize, CByte(0))
                            ElseIf mappedSpace + Page.pageSize * 8 * dbAllocationQuantum <= newFileSize Then
                                memset(page__1, 0, Page.pageSize, CByte(&HFF))
                            Else
                                Dim nBits As Integer = CInt((newFileSize - mappedSpace) >> dbAllocationQuantumBits)
                                memset(page__1, 0, nBits >> 3, CByte(&HFF))
                                page__1(nBits >> 3) = CByte((1 << (nBits And 7)) - 1)
                                memset(page__1, (nBits >> 3) + 1, Page.pageSize - (nBits >> 3) - 1, CByte(0))
                            End If
                            stream.Write(page__1, 0, Page.pageSize)
                        Else
                            Dim pg As Page = pool.getPage(pos And Not dbFlagsMask)
                            stream.Write(pg.data, 0, Page.pageSize)
                            pool.unfix(pg)
                        End If
                    End If
                Next

                For i = 0 To nObjects - 1
                    Dim pos As Long = index(i)
                    If pos <> 0 AndAlso (CInt(pos) And (dbFreeHandleFlag Or dbPageObjectFlag)) = 0 Then
                        pos = pos And Not dbFlagsMask
                        Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
                        Dim pg As Page = pool.getPage(pos - offs)
                        Dim size As Integer = ObjectHeader.getSize(pg.data, offs)
                        size = (size + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)

                        While True
                            If Page.pageSize - offs >= size Then
                                stream.Write(pg.data, offs, size)
                                Exit While
                            End If
                            stream.Write(pg.data, offs, Page.pageSize - offs)
                            size -= Page.pageSize - offs
                            pos += Page.pageSize - offs
                            offs = 0
                            pool.unfix(pg)
                            pg = pool.getPage(pos)
                        End While
                        pool.unfix(pg)
                    End If
                Next
                If recOffs <> newFileSize Then
                    Debug.Assert(newFileSize - recOffs < Page.pageSize)
                    Dim align As Integer = CInt(newFileSize - recOffs)
                    memset(page__1, 0, align, CByte(0))
                    stream.Write(page__1, 0, align)
                End If
            End SyncLock
        End Sub

        Public Function CreateIndex(Of K, V As {Class, IPersistent})(indexType As IndexType) As IIndex(Of K, V) Implements IDatabase.CreateIndex
            SyncLock Me
                ensureOpened()
#If WITH_OLD_BTREE Then
				Dim index As IIndex(Of K, V) = If(m_alternativeBtree, New Btree(Of K, V)(indexType), DirectCast(New OldBtree(Of K, V)(indexType), IIndex(Of K, V)))
#Else
                Dim index As IIndex(Of K, V) = New Btree(Of K, V)(indexType)
#End If
                index.AssignOid(Me, 0, False)
                Return index
            End SyncLock
        End Function

        Public Function CreateThickIndex(Of K, V As {Class, IPersistent})() As IIndex(Of K, V) Implements IDatabase.CreateThickIndex
            SyncLock Me
                ensureOpened()
                Return New ThickIndex(Of K, V)(Me)
            End SyncLock
        End Function

		#If WITH_OLD_BTREE Then
		Public Function CreateBitIndex(Of T As {Class, IPersistent})() As IBitIndex(Of T)
			SyncLock Me
				ensureOpened()
				Dim index As IBitIndex(Of T) = New OldBitIndexImpl(Of T)()
				index.AssignOid(Me, 0, False)
				Return index
			End SyncLock
		End Function
		#End If

        Public Function CreateSpatialIndex(Of T As {Class, IPersistent})() As ISpatialIndex(Of T) Implements IDatabase.CreateSpatialIndex
            SyncLock Me
                ensureOpened()
                Dim index As New Rtree(Of T)()
                index.AssignOid(Me, 0, False)
                Return index
            End SyncLock
        End Function

        Public Function CreateSpatialIndexR2(Of T As {Class, IPersistent})() As ISpatialIndexR2(Of T) Implements IDatabase.CreateSpatialIndexR2
            SyncLock Me
                ensureOpened()
                Dim index As New RtreeR2(Of T)()
                index.AssignOid(Me, 0, False)
                Return index
            End SyncLock
        End Function

        Public Function CreateSortedCollection(Of K, V As {Class, IPersistent})(comparator As PersistentComparator(Of K, V), indexType__1 As IndexType) As ISortedCollection(Of K, V) Implements IDatabase.CreateSortedCollection
            ensureOpened()
            Dim unique As Boolean = (indexType__1 = IndexType.Unique)
            Return New Ttree(Of K, V)(comparator, unique)
        End Function

        Public Function CreateSortedCollection(Of K, V As {Class, IPersistent, IComparable(Of K), IComparable(Of V)})(indexType__1 As IndexType) As ISortedCollection(Of K, V) Implements IDatabase.CreateSortedCollection
            ensureOpened()
            Dim unique As Boolean = (indexType__1 = IndexType.Unique)
            Return New Ttree(Of K, V)(New DefaultPersistentComparator(Of K, V)(), unique)
        End Function

		Friend Function CreateBtreeSet(Of T As {Class, IPersistent})() As ISet(Of T)
			SyncLock Me
				ensureOpened()
				#If WITH_OLD_BTREE Then
				Dim s As ISet(Of T) = If(m_alternativeBtree, DirectCast(New PersistentSet(Of T)(), ISet(Of T)), DirectCast(New OldPersistentSet(Of T)(), ISet(Of T)))
				#Else
				Dim s As ISet(Of T) = New PersistentSet(Of T)()
				#End If
				s.AssignOid(Me, 0, False)
				Return s
			End SyncLock
		End Function

        Public Function CreateSet(Of T As {Class, IPersistent})() As ISet(Of T) Implements IDatabase.CreateSet
            Return CreateSet(Of T)(8)
        End Function

        Public Function CreateSet(Of T As {Class, IPersistent})(initialSize As Integer) As ISet(Of T) Implements IDatabase.CreateSet
            SyncLock Me
                ensureOpened()
                Return New ScalableSet(Of T)(Me, initialSize)
            End SyncLock
        End Function

        Public Function CreateFieldIndex(Of K, V As {Class, IPersistent})(fieldName As [String], indexType__1 As IndexType) As IFieldIndex(Of K, V) Implements IDatabase.CreateFieldIndex
            SyncLock Me
                ensureOpened()
                Dim unique As Boolean = (indexType__1 = IndexType.Unique)
#If WITH_OLD_BTREE Then
				Dim index As IFieldIndex(Of K, V) = If(m_alternativeBtree, DirectCast(New BtreeFieldIndex(Of K, V)(fieldName, unique), IFieldIndex(Of K, V)), DirectCast(New OldBtreeFieldIndex(Of K, V)(fieldName, unique), IFieldIndex(Of K, V)))
#Else
                Dim index As IFieldIndex(Of K, V) = DirectCast(New BtreeFieldIndex(Of K, V)(fieldName, unique), IFieldIndex(Of K, V))
#End If
                index.AssignOid(Me, 0, False)
                Return index
            End SyncLock
        End Function

        Public Function CreateFieldIndex(Of T As {Class, IPersistent})(fieldNames As String(), indexType__1 As IndexType) As IMultiFieldIndex(Of T) Implements IDatabase.CreateFieldIndex
            SyncLock Me
                ensureOpened()
                Dim unique As Boolean = (indexType__1 = IndexType.Unique)
#If CF Then
				If m_alternativeBtree Then
					Throw New DatabaseError(DatabaseError.ErrorCode.UNSUPPORTED_INDEX_TYPE)
				End If
				Dim index As MultiFieldIndex(Of T) = New BtreeMultiFieldIndex(Of T)(fieldNames, unique)
#Else
#If WITH_OLD_BTREE Then
				Dim index As IMultiFieldIndex(Of T) = If(m_alternativeBtree, DirectCast(New BtreeMultiFieldIndex(Of T)(fieldNames, unique), IMultiFieldIndex(Of T)), DirectCast(New OldBtreeMultiFieldIndex(Of T)(fieldNames, unique), IMultiFieldIndex(Of T)))
#Else
                Dim index As IMultiFieldIndex(Of T) = DirectCast(New BtreeMultiFieldIndex(Of T)(fieldNames, unique), IMultiFieldIndex(Of T))
#End If
#End If
                index.AssignOid(Me, 0, False)
                Return index
            End SyncLock
        End Function

        Public Function CreateLink(Of T As {Class, IPersistent})() As ILink(Of T) Implements IDatabase.CreateLink
            Return CreateLink(Of T)(8)
        End Function

        Public Function CreateLink(Of T As {Class, IPersistent})(initialSize As Integer) As ILink(Of T) Implements IDatabase.CreateLink
            Return New LinkImpl(Of T)(initialSize)
        End Function

        Friend Function ConstructLink(Of T As {Class, IPersistent})(arr As IPersistent(), owner As IPersistent) As ILink(Of T)
            Return New LinkImpl(Of T)(arr, owner)
        End Function

        Public Function CreateArray(Of T As {Class, IPersistent})() As IPArray(Of T) Implements IDatabase.CreateArray
            Return CreateArray(Of T)(8)
        End Function

        Public Function CreateArray(Of T As {Class, IPersistent})(initialSize As Integer) As IPArray(Of T) Implements IDatabase.CreateArray
            Return New PArrayImpl(Of T)(Me, initialSize)
        End Function

		Friend Function ConstructArray(Of T As {Class, IPersistent})(arr As Integer(), owner As IPersistent) As IPArray(Of T)
			Return New PArrayImpl(Of T)(Me, arr, owner)
		End Function

        Public Function CreateRelation(Of M As {Class, IPersistent}, O As {Class, IPersistent})(owner As O) As Relation(Of M, O) Implements IDatabase.CreateRelation
            Return New RelationImpl(Of M, O)(owner)
        End Function

        Public Function CreateTimeSeries(Of T As ITimeSeriesTick)(blockSize As Integer, maxBlockTimeInterval As Long) As ITimeSeries(Of T) Implements IDatabase.CreateTimeSeries
            Return New TimeSeriesImpl(Of T)(Me, blockSize, maxBlockTimeInterval)
        End Function

		#If WITH_PATRICIA Then
		Public Function CreatePatriciaTrie(Of T As {Class, IPersistent})() As IPatriciaTrie(Of T)
			Return New PTrie(Of T)()
		End Function
		#End If

        Public Function CreateBlob() As IBlob Implements IDatabase.CreateBlob
            Return New BlobImpl(Page.pageSize - ObjectHeader.Sizeof - 16)
        End Function

		#If WITH_XML Then
		Public Sub ExportXML(writer As System.IO.StreamWriter)
			SyncLock Me
				ensureOpened()
				Dim rootOid As Integer = header.root(1 - currIndex).rootObject
				If rootOid <> 0 Then
					Dim xmlExporter As New XmlExporter(Me, writer)
					xmlExporter.exportDatabase(rootOid)
				End If
			End SyncLock
		End Sub

		Public Sub ImportXML(reader As System.IO.StreamReader)
			SyncLock Me
				ensureOpened()
				Dim xmlImporter As New XmlImporter(Me, reader)
				xmlImporter.importDatabase()
			End SyncLock
		End Sub
		#End If

		Friend Function getGCPos(oid As Integer) As Long
			Dim pg As Page = pool.getPage(header.root(currIndex).index + (CLng(oid >> dbHandlesPerPageBits) << Page.pageBits))
			Dim pos As Long = Bytes.unpack8(pg.data, (oid And (dbHandlesPerPage - 1)) << 3)
			pool.unfix(pg)
			Return pos
		End Function

		Friend Sub markOid(oid As Integer)
			If 0 = oid Then
				Return
			End If
			Dim pos As Long = getGCPos(oid)
			If (pos And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
			End If

			Dim bit As Integer = CInt(CULng(pos) >> dbAllocationQuantumBits)
			If (blackBitmap(CUInt(bit) >> 5) And (1 << (bit And 31))) = 0 Then
				greyBitmap(CUInt(bit) >> 5) = greyBitmap(CUInt(bit) >> 5) Or 1 << (bit And 31)
			End If
		End Sub

		Friend Function getGCPage(oid As Integer) As Page
			Return pool.getPage(getGCPos(oid) And Not dbFlagsMask)
		End Function

        Public Function Gc() As Integer Implements IDatabase.Gc
            SyncLock Me
                Return gc0()
            End SyncLock
        End Function

		#If WITH_OLD_BTREE Then
		Friend Function createBtreeStub(data As Byte(), offs As Integer) As OldBtree
			Return New OldBtree(Of Integer, IPersistent)(data, ObjectHeader.Sizeof + offs)
		End Function
		#End If

		Private Sub mark()
			' Console.WriteLine("Start GC, allocatedDelta=" + allocatedDelta + ", header[" + currIndex + "].size=" + header.root[currIndex].size + ", gcTreshold=" + gcThreshold);
			Dim bitmapSize As Integer = CInt(CULng(header.root(currIndex).size) >> (dbAllocationQuantumBits + 5)) + 1
			Dim existsNotMarkedObjects As Boolean
			Dim pos As Long
			Dim i As Integer, j As Integer

			If Listener IsNot Nothing Then
				Listener.GcStarted()
			End If

			greyBitmap = New Integer(bitmapSize - 1) {}
			blackBitmap = New Integer(bitmapSize - 1) {}
			Dim rootOid As Integer = header.root(currIndex).rootObject
			If rootOid <> 0 Then
				markOid(rootOid)
				Do
					existsNotMarkedObjects = False
					For i = 0 To bitmapSize - 1
						If greyBitmap(i) <> 0 Then
							existsNotMarkedObjects = True
							For j = 0 To 31
								If (greyBitmap(i) And (1 << j)) <> 0 Then
									pos = ((CLng(i) << 5) + j) << dbAllocationQuantumBits
									greyBitmap(i) = greyBitmap(i) And Not (1 << j)
									blackBitmap(i) = blackBitmap(i) Or 1 << j
									Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
									Dim pg As Page = pool.getPage(pos - offs)
									Dim typeOid As Integer = ObjectHeader.[getType](pg.data, offs)
									If typeOid <> 0 Then
										Dim desc As ClassDescriptor = DirectCast(lookupObject(typeOid, GetType(ClassDescriptor)), ClassDescriptor)
										#If WITH_OLD_BTREE Then
										If GetType(OldBtree).IsAssignableFrom(desc.cls) Then
											Dim btree As OldBtree = createBtreeStub(pg.data, offs)
											btree.AssignOid(Me, 0, False)
											btree.markTree()
										#End If
										ElseIf desc.hasReferences Then
											markObject(pool.[get](pos), ObjectHeader.Sizeof, desc)
										End If
									End If
									pool.unfix(pg)
								End If
							Next
						End If
					Next
				Loop While existsNotMarkedObjects
			End If
		End Sub

		Private Function sweep() As Integer
			Dim nDeallocated As Integer = 0
			Dim pos As Long
			gcDone = True
			Dim i As Integer = dbFirstUserId, j As Integer = committedIndexSize
			While i < j
				pos = getGCPos(i)
				If pos <> 0 AndAlso (CInt(pos) And (dbPageObjectFlag Or dbFreeHandleFlag)) = 0 Then
					Dim bit As Integer = CInt(CULng(pos) >> dbAllocationQuantumBits)
					If (blackBitmap(CUInt(bit) >> 5) And (1 << (bit And 31))) = 0 Then
						' object is not accessible
						If getPos(i) <> pos Then
							Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
						End If

						Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
						Dim pg As Page = pool.getPage(pos - offs)
						Dim typeOid As Integer = ObjectHeader.[getType](pg.data, offs)
						If typeOid <> 0 Then
							Dim desc As ClassDescriptor = findClassDescriptor(typeOid)
							nDeallocated += 1
							#If WITH_OLD_BTREE Then
							If desc IsNot Nothing AndAlso (GetType(OldBtree).IsAssignableFrom(desc.cls)) Then
								Dim btree As OldBtree = createBtreeStub(pg.data, offs)
								pool.unfix(pg)
								btree.AssignOid(Me, i, False)
								btree.Deallocate()
							Else
								#End If
								Dim size As Integer = ObjectHeader.getSize(pg.data, offs)
								pool.unfix(pg)
								freeId(i)
								objectCache.Remove(i)
								cloneBitmap(pos, size)
							End If
							If Listener IsNot Nothing Then
								Listener.DeallocateObject(desc.cls, i)
							End If
						End If
					End If
				End If
				i += 1
			End While

			greyBitmap = Nothing
			blackBitmap = Nothing
			allocatedDelta = 0
			gcActive = False

			If Listener IsNot Nothing Then
				Listener.GcCompleted(nDeallocated)
			End If

			Return nDeallocated
		End Function

		#If Not CF Then
		Public Sub backgroundGcThread()
			While True
				SyncLock backgroundGcStartMonitor
					While Not gcGo AndAlso opened
						Monitor.Wait(backgroundGcStartMonitor)
					End While
					If Not opened Then
						Return
					End If

					gcGo = False
				End SyncLock
				SyncLock backgroundGcMonitor
					If Not opened Then
						Return
					End If

					mark()
					SyncLock Me
						SyncLock objectCache
							sweep()
						End SyncLock
					End SyncLock
				End SyncLock
			End While
		End Sub

		Private Sub activateGc()
			SyncLock backgroundGcStartMonitor
				gcGo = True
				Monitor.Pulse(backgroundGcStartMonitor)
			End SyncLock
		End Sub
		#End If

		Private Function gc0() As Integer
			SyncLock objectCache
				ensureOpened()

				If gcDone OrElse gcActive Then
					Return 0
				End If

				gcActive = True
				#If Not CF Then
				If m_backgroundGc Then
					If gcThread Is Nothing Then
						gcThread = New Thread(New ThreadStart(AddressOf backgroundGcThread))
						gcThread.Start()
					End If
					activateGc()
					Return 0
				End If
				#End If
				' System.out.println("Start GC, allocatedDelta=" + allocatedDelta + ", header[" + currIndex + "].size=" + header.root[currIndex].size + ", gcTreshold=" + gcThreshold);

				mark()
				Return sweep()
			End SyncLock
		End Function

        Public Function GetMemoryUsage() As Dictionary(Of Type, TypeMemoryUsage) Implements IDatabase.GetMemoryUsage
            SyncLock Me
                SyncLock objectCache
                    ensureOpened()

                    Dim bitmapSize As Integer = CInt(header.root(currIndex).size >> (dbAllocationQuantumBits + 5)) + 1
                    Dim existsNotMarkedObjects As Boolean
                    Dim pos As Long
                    Dim i As Integer, j As Integer

                    ' mark
                    greyBitmap = New Integer(bitmapSize - 1) {}
                    blackBitmap = New Integer(bitmapSize - 1) {}
                    Dim rootOid As Integer = header.root(currIndex).rootObject
                    Dim map = New Dictionary(Of Type, TypeMemoryUsage)()
                    If 0 = rootOid Then
                        Return map
                    End If

                    Dim indexUsage As New TypeMemoryUsage(GetType(IGenericIndex))
                    Dim classUsage As New TypeMemoryUsage(GetType(Type))

                    markOid(rootOid)
                    Do
                        existsNotMarkedObjects = False
                        For i = 0 To bitmapSize - 1
                            If greyBitmap(i) <> 0 Then
                                existsNotMarkedObjects = True
                                For j = 0 To 31
                                    If (greyBitmap(i) And (1 << j)) <> 0 Then
                                        pos = ((CLng(i) << 5) + j) << dbAllocationQuantumBits
                                        greyBitmap(i) = greyBitmap(i) And Not (1 << j)
                                        blackBitmap(i) = blackBitmap(i) Or 1 << j
                                        Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
                                        Dim pg As Page = pool.getPage(pos - offs)
                                        Dim typeOid As Integer = ObjectHeader.[GetType](pg.data, offs)
                                        Dim objSize As Integer = ObjectHeader.getSize(pg.data, offs)
                                        Dim alignedSize As Integer = (objSize + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)
                                        If typeOid <> 0 Then
                                            markOid(typeOid)
                                            Dim desc As ClassDescriptor = findClassDescriptor(typeOid)
#If WITH_OLD_BTREE Then
											If GetType(OldBtree).IsAssignableFrom(desc.cls) Then
												Dim btree As OldBtree = createBtreeStub(pg.data, offs)
												btree.AssignOid(Me, 0, False)
												Dim nPages As Integer = btree.markTree()
												indexUsage.Count += 1
												indexUsage.TotalSize += CLng(nPages) * Page.pageSize + objSize
												indexUsage.AllocatedSize += CLng(nPages) * Page.pageSize + alignedSize
											Else
#End If
                                            Dim usage As TypeMemoryUsage
                                            Dim ok = map.TryGetValue(desc.cls, usage)
                                            If Not ok Then
                                                usage = New TypeMemoryUsage(desc.cls)
                                                map(desc.cls) = usage
                                            End If
                                            usage.Count += 1
                                            usage.TotalSize += objSize
                                            usage.AllocatedSize += alignedSize

                                            If desc.hasReferences Then
                                                markObject(pool.[get](pos), ObjectHeader.Sizeof, desc)
                                            End If
                                        End If
                                    Else
                                        classUsage.Count += 1
                                        classUsage.TotalSize += objSize
                                        classUsage.AllocatedSize += alignedSize
                                    End If
                                    pool.unfix(pg)
									End If
                        Next
							End If
						Next
                    Loop While existsNotMarkedObjects

                    If indexUsage.Count <> 0 Then
                        map(GetType(IGenericIndex)) = indexUsage
                    End If

                    If classUsage.Count <> 0 Then
                        map(GetType(Type)) = classUsage
                    End If

                    Dim system As New TypeMemoryUsage(GetType(IDatabase))
                    system.TotalSize += header.root(0).indexSize * 8L
                    system.TotalSize += header.root(1).indexSize * 8L
                    system.TotalSize += CLng(header.root(currIndex).bitmapEnd - dbBitmapId) * Page.pageSize
                    system.TotalSize += Page.pageSize
                    ' root page
                    If header.root(currIndex).bitmapExtent <> 0 Then
                        system.AllocatedSize = getBitmapUsedSpace(dbBitmapId, dbBitmapId + dbBitmapPages) + getBitmapUsedSpace(header.root(currIndex).bitmapExtent, header.root(currIndex).bitmapExtent + header.root(currIndex).bitmapEnd - dbBitmapId)
                    Else
                        system.AllocatedSize = getBitmapUsedSpace(dbBitmapId, header.root(currIndex).bitmapEnd)
                    End If
                    system.Count = header.root(currIndex).indexSize
                    map(GetType(IDatabase)) = system
                    Return map
                End SyncLock
            End SyncLock
        End Function

		Private Function getBitmapUsedSpace(from As Integer, till As Integer) As Long
			Dim allocated As Long = 0
			While from < till
				Dim pg As Page = getGCPage(from)
				For j As Integer = 0 To Page.pageSize - 1
					Dim mask As Integer = pg.data(j) And &Hff
					While mask <> 0
						If (mask And 1) <> 0 Then
							allocated += dbAllocationQuantum
						End If

						mask >>= 1
					End While
				Next
				pool.unfix(pg)
				from += 1
			End While
			Return allocated
		End Function

		Friend Function markObject(obj As Byte(), offs As Integer, desc As ClassDescriptor) As Integer
			Dim all As ClassDescriptor.FieldDescriptor() = desc.allFields

			Dim i As Integer = 0, n As Integer = all.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = all(i)
				Select Case fd.type
					Case ClassDescriptor.FieldType.tpBoolean, ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte
						offs += 1
						Continue Select
					Case ClassDescriptor.FieldType.tpChar, ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort
						offs += 2
						Continue Select
					Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpFloat
						offs += 4
						Continue Select
					Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong, ClassDescriptor.FieldType.tpDouble, ClassDescriptor.FieldType.tpDate
						offs += 8
						Continue Select
					Case ClassDescriptor.FieldType.tpDecimal, ClassDescriptor.FieldType.tpGuid
						offs += 16
						Continue Select
					Case ClassDescriptor.FieldType.tpString
						If True Then
							Dim strlen As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If strlen > 0 Then
								offs += strlen * 2
							ElseIf strlen < -1 Then
								offs -= strlen + 2
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
						markOid(Bytes.unpack4(obj, offs))
						offs += 4
						Continue Select
					Case ClassDescriptor.FieldType.tpValue
						offs = markObject(obj, offs, fd.valueDesc)
						Continue Select
					Case ClassDescriptor.FieldType.tpRaw
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If len > 0 Then
								offs += len
							ElseIf len = -2 - CInt(ClassDescriptor.FieldType.tpObject) Then
								markOid(Bytes.unpack4(obj, offs))
								offs += 4
							ElseIf len < -1 Then
								offs += ClassDescriptor.Sizeof(-2 - len)
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfByte, ClassDescriptor.FieldType.tpArrayOfSByte, ClassDescriptor.FieldType.tpArrayOfBoolean
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If len > 0 Then
								offs += len
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfShort, ClassDescriptor.FieldType.tpArrayOfUShort, ClassDescriptor.FieldType.tpArrayOfChar
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If len > 0 Then
								offs += len * 2
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfInt, ClassDescriptor.FieldType.tpArrayOfUInt, ClassDescriptor.FieldType.tpArrayOfEnum, ClassDescriptor.FieldType.tpArrayOfFloat
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If len > 0 Then
								offs += len * 4
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfLong, ClassDescriptor.FieldType.tpArrayOfULong, ClassDescriptor.FieldType.tpArrayOfDouble, ClassDescriptor.FieldType.tpArrayOfDate
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							If len > 0 Then
								offs += len * 8
							End If
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfString
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								Dim strlen As Integer = Bytes.unpack4(obj, offs)
								offs += 4
								If strlen > 0 Then
									offs += strlen * 2
								ElseIf strlen < -1 Then
									offs -= strlen + 2
								End If
							End While
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfObject, ClassDescriptor.FieldType.tpArrayOfOid, ClassDescriptor.FieldType.tpLink
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								markOid(Bytes.unpack4(obj, offs))
								offs += 4
							End While
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfValue
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							Dim valueDesc As ClassDescriptor = fd.valueDesc
							While System.Threading.Interlocked.Decrement(len) >= 0
								offs = markObject(obj, offs, valueDesc)
							End While
							Continue Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfRaw
						If True Then
							Dim len As Integer = Bytes.unpack4(obj, offs)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								Dim rawlen As Integer = Bytes.unpack4(obj, offs)
								offs += 4
								If rawlen >= 0 Then
									offs += rawlen
								ElseIf rawlen = -2 - CInt(ClassDescriptor.FieldType.tpObject) Then
									markOid(Bytes.unpack4(obj, offs))
									offs += 4
								ElseIf rawlen < -1 Then
									offs += ClassDescriptor.Sizeof(-2 - rawlen)
								End If
							End While
							Continue Select
						End If
				End Select
				i += 1
			End While
			Return offs
		End Function

		Friend Class ThreadTransactionContext
			Friend nested As Integer
			Friend locked As ArrayList
			Friend modified As ArrayList

			Friend Sub New()
				locked = New ArrayList()
				modified = New ArrayList()
			End Sub
		End Class

		Friend Shared ReadOnly Property TransactionContext() As ThreadTransactionContext
			Get
				Dim ctx As ThreadTransactionContext = DirectCast(Thread.GetData(m_transactionContext), ThreadTransactionContext)
				If ctx Is Nothing Then
					ctx = New ThreadTransactionContext()
					Thread.SetData(m_transactionContext, ctx)
				End If
				Return ctx
			End Get
		End Property

        Public Sub EndThreadTransaction() Implements IDatabase.EndThreadTransaction
            EndThreadTransaction(Int32.MaxValue)
        End Sub

		#If CF Then
		Public Sub RegisterAssembly(assembly As System.Reflection.Assembly)
			assemblies.Add(assembly)
		End Sub

		Public Sub BeginThreadTransaction(mode As TransactionMode)
			If mode = TransactionMode.Serializable Then
				useSerializableTransactions = True
				TransactionContext.nested += 1
				

			Else
				transactionMonitor.Enter()
				Try
					If scheduledCommitTime <> Int64.MaxValue Then
						nBlockedTransactions += 1
						While DateTime.Now.Ticks >= scheduledCommitTime
							transactionMonitor.Wait()
						End While
						nBlockedTransactions -= 1
					End If
					nNestedTransactions += 1
				Finally
					transactionMonitor.[Exit]()
				End Try
				If mode = TransactionMode.Exclusive Then
					transactionLock.ExclusiveLock()
				Else
					transactionLock.SharedLock()
				End If
			End If
		End Sub

		Public Sub EndThreadTransaction(maxDelay As Integer)
			Dim ctx As ThreadTransactionContext = TransactionContext
			If ctx.nested <> 0 Then
				' serializable transaction
				If System.Threading.Interlocked.Decrement(ctx.nested) = 0 Then
					Dim i As Integer = ctx.modified.Count
					If i <> 0 Then
						Do
							DirectCast(ctx.modified(System.Threading.Interlocked.Decrement(i)), IPersistent).Store()
						Loop While i <> 0

						SyncLock backgroundGcMonitor
							SyncLock Me
								commit0()
							End SyncLock
						End SyncLock
					End If
					i = ctx.locked.Count
					While System.Threading.Interlocked.Decrement(i) >= 0
						DirectCast(ctx.locked(i), IResource).Reset()
					End While
					ctx.modified.Clear()
					ctx.locked.Clear()
				End If
			Else
				' exclusive or cooperative transaction        
				transactionMonitor.Enter()
				Try
					transactionLock.Unlock()
					If nNestedTransactions <> 0 Then
						' may be everything is already aborted
						If System.Threading.Interlocked.Decrement(nNestedTransactions) = 0 Then
							nCommittedTransactions += 1
							Commit()
							scheduledCommitTime = Int64.MaxValue
							If nBlockedTransactions <> 0 Then
								transactionMonitor.PulseAll()
							End If
						Else
							If maxDelay <> Int32.MaxValue Then
								Dim nextCommit As Long = DateTime.Now.Ticks + maxDelay
								If nextCommit < scheduledCommitTime Then
									scheduledCommitTime = nextCommit
								End If
								If maxDelay = 0 Then
									Dim n As Integer = nCommittedTransactions
									nBlockedTransactions += 1
									Do
										transactionMonitor.Wait()
									Loop While nCommittedTransactions = n
									nBlockedTransactions -= 1
								End If
							End If
						End If
					End If
				Finally
					transactionMonitor.[Exit]()
				End Try
			End If
		End Sub

		Public Sub RollbackThreadTransaction()
			Dim ctx As ThreadTransactionContext = TransactionContext
			If ctx.nested <> 0 Then
				' serializable transaction
				ctx.nested = 0
				Dim i As Integer = ctx.modified.Count
				If i <> 0 Then
					Do
						DirectCast(ctx.modified(System.Threading.Interlocked.Decrement(i)), IPersistent).Invalidate()
					Loop While i <> 0

					SyncLock Me
						rollback0()
					End SyncLock
				End If
				i = ctx.locked.Count
				While System.Threading.Interlocked.Decrement(i) >= 0
					DirectCast(ctx.locked(i), IResource).Reset()
				End While
				ctx.modified.Clear()
				ctx.locked.Clear()
			Else
				Try
					transactionMonitor.Enter()
					transactionLock.Reset()
					nNestedTransactions = 0
					If nBlockedTransactions <> 0 Then
						transactionMonitor.PulseAll()
					End If
					Rollback()
				Finally
					transactionMonitor.[Exit]()
				End Try
			End If
		End Sub
		#Else
		Public Overridable Sub BeginThreadTransaction(mode As TransactionMode)
			If mode = TransactionMode.Serializable Then
				useSerializableTransactions = True
				TransactionContext.nested += 1
				

			Else
				SyncLock transactionMonitor
					If scheduledCommitTime <> Int64.MaxValue Then
						nBlockedTransactions += 1
						While DateTime.Now.Ticks >= scheduledCommitTime
							Monitor.Wait(transactionMonitor)
						End While
						nBlockedTransactions -= 1
					End If
					nNestedTransactions += 1
				End SyncLock

				If mode = TransactionMode.Exclusive Then
					transactionLock.ExclusiveLock()
				Else
					transactionLock.SharedLock()
				End If
			End If
		End Sub

        Public Overridable Sub EndThreadTransaction(maxDelay As Integer) Implements IDatabase.EndThreadTransaction
            Dim ctx As ThreadTransactionContext = TransactionContext
            If ctx.nested <> 0 Then
                ' serializable transaction
                If System.Threading.Interlocked.Decrement(ctx.nested) = 0 Then
                    Dim i As Integer = ctx.modified.Count
                    If i <> 0 Then
                        Do
                            DirectCast(ctx.modified(System.Threading.Interlocked.Decrement(i)), IPersistent).Store()
                        Loop While i <> 0

                        SyncLock backgroundGcMonitor
                            SyncLock Me
                                commit0()
                            End SyncLock
                        End SyncLock
                    End If
                    i = ctx.locked.Count
                    While System.Threading.Interlocked.Decrement(i) >= 0
                        DirectCast(ctx.locked(i), IResource).Reset()
                    End While
                    ctx.modified.Clear()
                    ctx.locked.Clear()
                End If
            Else
                ' exclusive or cooperative transaction        
                SyncLock transactionMonitor
                    transactionLock.Unlock()
                    If nNestedTransactions <> 0 Then
                        ' may be everything is already aborted
                        If System.Threading.Interlocked.Decrement(nNestedTransactions) = 0 Then
                            nCommittedTransactions += 1
                            Commit()
                            scheduledCommitTime = Int64.MaxValue
                            If nBlockedTransactions <> 0 Then
                                Monitor.PulseAll(transactionMonitor)
                            End If
                        Else
                            If maxDelay <> Int32.MaxValue Then
                                Dim nextCommit As Long = DateTime.Now.Ticks + maxDelay
                                If nextCommit < scheduledCommitTime Then
                                    scheduledCommitTime = nextCommit
                                End If
                                If maxDelay = 0 Then
                                    Dim n As Integer = nCommittedTransactions
                                    nBlockedTransactions += 1
                                    Do
                                        Monitor.Wait(transactionMonitor)
                                    Loop While nCommittedTransactions = n
                                    nBlockedTransactions -= 1
                                End If
                            End If
                        End If
                    End If
                End SyncLock
            End If
        End Sub

        Public Sub RollbackThreadTransaction() Implements IDatabase.RollbackThreadTransaction
            Dim ctx As ThreadTransactionContext = TransactionContext
            If ctx.nested <> 0 Then
                ' serializable transaction
                ctx.nested = 0
                Dim i As Integer = ctx.modified.Count
                If i <> 0 Then
                    Do
                        DirectCast(ctx.modified(System.Threading.Interlocked.Decrement(i)), IPersistent).Invalidate()
                    Loop While i <> 0

                    SyncLock Me
                        rollback0()
                    End SyncLock
                End If
                i = ctx.locked.Count
                While System.Threading.Interlocked.Decrement(i) >= 0
                    DirectCast(ctx.locked(i), IResource).Reset()
                End While
                ctx.modified.Clear()
                ctx.locked.Clear()
            Else
                SyncLock transactionMonitor
                    transactionLock.Reset()
                    nNestedTransactions = 0
                    If nBlockedTransactions <> 0 Then
                        Monitor.PulseAll(transactionMonitor)
                    End If

                    Rollback()
                End SyncLock
            End If
        End Sub

		#End If

        Public Overridable Sub Close() Implements IDatabase.Close
            SyncLock backgroundGcMonitor
                Try
                    Commit()
                Catch
                    opened = False
                    Throw
                End Try
                opened = False
            End SyncLock
#If Not CF Then
            If codeGenerationThread IsNot Nothing Then
                codeGenerationThread.Join()
                codeGenerationThread = Nothing
            End If

            If gcThread IsNot Nothing Then
                activateGc()
                gcThread.Join()
            End If
#End If
            If isDirty() Then
                Dim pg As Page = pool.putPage(0)
                header.pack(pg.data)
                pool.flush()
                pool.modify(pg)
                header.dirty = False
                header.pack(pg.data)
                pool.unfix(pg)
                pool.flush()
            End If
            pool.close()
            pool = Nothing
            objectCache = Nothing
            classDescMap = Nothing
            resolvedTypes = Nothing
            bitmapPageAvailableSpace = Nothing
            dirtyPagesMap = Nothing
            descList = Nothing
        End Sub

		#If WITH_OLD_BTREE Then
		Public Property AlternativeBtree() As Boolean
			Get
				Return m_alternativeBtree
			End Get
			Set
				m_alternativeBtree = value
			End Set
		End Property
		#End If

		' TODO: needs tests
		Public Property ObjectIndexInitSize() As Integer
			Get
				Return initIndexSize
			End Get
			Set
				initIndexSize = value
			End Set
		End Property

		' TODO: needs tests
		Public Property ExtensionQuantum() As Long
			Get
				Return m_extensionQuantum
			End Get
			Set
				m_extensionQuantum = value
			End Set
		End Property

		' TODO: needs tests
		Public Property GcThreshold() As Long
			Get
				Return m_gcThreshold
			End Get
			Set
				m_gcThreshold = value
			End Set
		End Property

		Public Property CodeGeneration() As Boolean
			Get
				Return enableCodeGeneration
			End Get
			Set
				enableCodeGeneration = value
			End Set
		End Property

		Public Property BackgroundGc() As Boolean
			Get
				Return m_backgroundGc
			End Get
			Set
				m_backgroundGc = value
			End Set
		End Property

		#If WITH_REPLICATION Then
		Public Property ReplicationAck() As Boolean
			Get
				Return m_replicationAck
			End Get
			Set
				m_replicationAck = value
			End Set
		End Property
		#End If

		' TODO: needs tests
		Public Property ObjectCacheInitSize() As Integer
			Get
				Return m_objectCacheInitSize
			End Get
			Set
				m_objectCacheInitSize = value
			End Set
		End Property

		'TODO: needs tests
		Public Property CacheKind() As CacheType
			Get
				Return m_cacheKind
			End Get
			Set
				m_cacheKind = value
			End Set
		End Property

		Public Property Listener() As DatabaseListener
			Get
				Return m_Listener
			End Get
			Set
				m_Listener = Value
			End Set
		End Property
		Private m_Listener As DatabaseListener

		Public ReadOnly Property File() As IFile
			Get
				Return If(pool Is Nothing, Nothing, pool.file)
			End Get
		End Property

		Public Function GetObjectByOid(oid As Integer) As IPersistent
			SyncLock Me
				Return If(oid = 0, Nothing, lookupObject(oid, Nothing))
			End SyncLock
		End Function

		Public Sub modifyObject(obj As IPersistent)
			SyncLock Me
				SyncLock objectCache
					If obj.IsModified() Then
						Return
					End If

					If useSerializableTransactions Then
						Dim ctx As ThreadTransactionContext = TransactionContext
						If ctx.nested <> 0 Then
							' serializable transaction
							ctx.modified.Add(obj)
						End If
					End If
					objectCache.SetDirty(obj.Oid)
				End SyncLock
			End SyncLock
		End Sub

		Public Sub lockObject(obj As IPersistent)
			If useSerializableTransactions Then
				Dim ctx As ThreadTransactionContext = TransactionContext
				If ctx.nested <> 0 Then
					' serializable transaction
					ctx.locked.Add(obj)
				End If
			End If
		End Sub

		Public Sub storeObject(obj As IPersistent)
			SyncLock Me
				ensureOpened()

				SyncLock objectCache
					storeObject0(obj)
				End SyncLock
			End SyncLock
		End Sub

		Public Sub storeFinalizedObject(obj As IPersistent)
			If Not opened Then
				Return
			End If
			SyncLock objectCache
				If obj.Oid <> 0 Then
					storeObject0(obj)
				End If
			End SyncLock
		End Sub

		Private Sub storeObject0(obj As IPersistent)
			obj.OnStore()
			Dim oid As Integer = obj.Oid
			Dim newObject As Boolean = False
			If oid = 0 Then
				oid = allocateId()
				If Not obj.IsDeleted() Then
					objectCache.Put(oid, obj)
				End If

				obj.AssignOid(Me, oid, False)
				newObject = True
			ElseIf obj.IsModified() Then
				objectCache.ClearDirty(oid)
			End If
			Dim data As Byte() = packObject(obj)
			Dim pos As Long
			Dim newSize As Integer = ObjectHeader.getSize(data, 0)
			If newObject OrElse (InlineAssignHelper(pos, getPos(oid))) = 0 Then
				pos = allocate(newSize, 0)
				setPos(oid, pos Or dbModifiedFlag)
			Else
				Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
				If (offs And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
					Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
				End If

				Dim pg As Page = pool.getPage(pos - offs)
				offs = offs And Not dbFlagsMask
				Dim size As Integer = ObjectHeader.getSize(pg.data, offs)
				pool.unfix(pg)
				If (pos And dbModifiedFlag) = 0 Then
					cloneBitmap(pos And Not dbFlagsMask, size)
					pos = allocate(newSize, 0)
					setPos(oid, pos Or dbModifiedFlag)
				Else
					If ((newSize + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)) > ((size + dbAllocationQuantum - 1) And Not (dbAllocationQuantum - 1)) Then
						Dim newPos As Long = allocate(newSize, 0)
						cloneBitmap(pos And Not dbFlagsMask, size)
						free(pos And Not dbFlagsMask, size)
						pos = newPos
						setPos(oid, pos Or dbModifiedFlag)
					ElseIf newSize < size Then
						ObjectHeader.setSize(data, 0, size)
					End If
				End If
			End If
			modified = True
			pool.put(pos And Not dbFlagsMask, data, newSize)
		End Sub

		Public Sub loadObject(obj As IPersistent)
			SyncLock Me
				If obj.IsRaw() Then
					loadStub(obj.Oid, obj, obj.[GetType]())
				End If
			End SyncLock
		End Sub

		Friend Function lookupObject(oid As Integer, cls As System.Type) As IPersistent
			Dim obj As IPersistent = objectCache.[Get](oid)
			If obj Is Nothing OrElse obj.IsRaw() Then
				obj = loadStub(oid, obj, cls)
			End If

			Return obj
		End Function

		Protected Overridable Function swizzle(obj As IPersistent) As Integer
			If obj Is Nothing Then
				Return 0
			End If

			If Not obj.IsPersistent() Then
				storeObject0(obj)
			End If

			Return obj.Oid
		End Function

		Friend Function findClassDescriptor(oid As Integer) As ClassDescriptor
			Return DirectCast(lookupObject(oid, GetType(ClassDescriptor)), ClassDescriptor)
		End Function

		Protected Overridable Function unswizzle(oid As Integer, cls As System.Type, recursiveLoading As Boolean) As IPersistent
			If oid = 0 Then
				Return Nothing
			End If

			If recursiveLoading Then
				Return lookupObject(oid, cls)
			End If

			Dim stub As IPersistent = objectCache.[Get](oid)
			If stub IsNot Nothing Then
				Return stub
			End If

			Dim desc As ClassDescriptor
			If cls Is GetType(Persistent) OrElse (InlineAssignHelper(desc, DirectCast(classDescMap(cls), ClassDescriptor))) Is Nothing OrElse desc.hasSubclasses Then
				Dim pos As Long = getPos(oid)
				Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
				If (offs And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
					Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
				End If

				Dim pg As Page = pool.getPage(pos - offs)
				Dim typeOid As Integer = ObjectHeader.[getType](pg.data, offs And Not dbFlagsMask)
				pool.unfix(pg)
				desc = findClassDescriptor(typeOid)
			End If
			If desc.serializer IsNot Nothing Then
				stub = desc.serializer.newInstance()
			Else
				stub = DirectCast(desc.newInstance(), IPersistent)
			End If

			stub.AssignOid(Me, oid, True)
			objectCache.Put(oid, stub)
			Return stub
		End Function

		Friend Function loadStub(oid As Integer, obj As IPersistent, cls As System.Type) As IPersistent
			Dim pos As Long = getPos(oid)
			If (pos And (dbFreeHandleFlag Or dbPageObjectFlag)) <> 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
			End If

			Dim body As Byte() = pool.[get](pos And Not dbFlagsMask)
			Dim desc As ClassDescriptor
			Dim typeOid As Integer = ObjectHeader.[getType](body, 0)
			If typeOid = 0 Then
				desc = classDescMap(cls)
			Else
				desc = findClassDescriptor(typeOid)
			End If

			If obj Is Nothing Then
				If desc.serializer IsNot Nothing Then
					obj = desc.serializer.newInstance()
				Else
					obj = DirectCast(desc.newInstance(), IPersistent)
				End If

				objectCache.Put(oid, obj)
			End If
			obj.AssignOid(Me, oid, False)
			If desc.serializer IsNot Nothing Then
				desc.serializer.unpack(Me, obj, body, obj.RecursiveLoading())
			Else
				unpackObject(obj, desc, obj.RecursiveLoading(), body, ObjectHeader.Sizeof, obj)
			End If

			obj.OnLoad()
			Return obj
		End Function

		Friend Function unpackObject(obj As Object, desc As ClassDescriptor, recursiveLoading As Boolean, body As Byte(), offs As Integer, po As IPersistent) As Integer
			Dim all As ClassDescriptor.FieldDescriptor() = desc.allFields
			Dim i As Integer = 0, n As Integer = all.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = all(i)
				If obj Is Nothing OrElse fd.field Is Nothing Then
					offs = skipField(body, offs, fd, fd.type)
				Else
					Dim val As Object = obj
					offs = unpackField(body, offs, recursiveLoading, val, fd, fd.type, _
						po)
					fd.field.SetValue(obj, val)
				End If
				i += 1
			End While
			Return offs
		End Function

		Public Function skipField(body As Byte(), offs As Integer, fd As ClassDescriptor.FieldDescriptor, type As ClassDescriptor.FieldType) As Integer
			Dim len As Integer
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean, ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte
					Return offs + 1
				Case ClassDescriptor.FieldType.tpChar, ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort
					Return offs + 2
				Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpFloat, ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
					Return offs + 4
				Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong, ClassDescriptor.FieldType.tpDouble, ClassDescriptor.FieldType.tpDate
					Return offs + 8
				Case ClassDescriptor.FieldType.tpDecimal, ClassDescriptor.FieldType.tpGuid
					Return offs + 16
				Case ClassDescriptor.FieldType.tpString
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						offs += len * 2
					ElseIf len < -1 Then
						offs -= len + 2
					End If

					Exit Select
				Case ClassDescriptor.FieldType.tpValue
					Return unpackObject(Nothing, fd.valueDesc, False, body, offs, Nothing)
				Case ClassDescriptor.FieldType.tpRaw, ClassDescriptor.FieldType.tpArrayOfByte, ClassDescriptor.FieldType.tpArrayOfSByte, ClassDescriptor.FieldType.tpArrayOfBoolean
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						offs += len
					ElseIf len < -1 Then
						offs += ClassDescriptor.Sizeof(-2 - len)
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfShort, ClassDescriptor.FieldType.tpArrayOfUShort, ClassDescriptor.FieldType.tpArrayOfChar
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						offs += len * 2
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfInt, ClassDescriptor.FieldType.tpArrayOfUInt, ClassDescriptor.FieldType.tpArrayOfFloat, ClassDescriptor.FieldType.tpArrayOfObject, ClassDescriptor.FieldType.tpArrayOfOid, ClassDescriptor.FieldType.tpLink
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						offs += len * 4
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfLong, ClassDescriptor.FieldType.tpArrayOfULong, ClassDescriptor.FieldType.tpArrayOfDouble, ClassDescriptor.FieldType.tpArrayOfDate
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						offs += len * 8
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfString
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						For j As Integer = 0 To len - 1
							Dim strlen As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If strlen > 0 Then
								offs += strlen * 2
							ElseIf strlen < -1 Then
								offs -= strlen + 2

							End If
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfValue
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						Dim valueDesc As ClassDescriptor = fd.valueDesc
						For j As Integer = 0 To len - 1
							offs = unpackObject(Nothing, valueDesc, False, body, offs, Nothing)
						Next
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfRaw
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len > 0 Then
						For j As Integer = 0 To len - 1
							Dim rawlen As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If rawlen > 0 Then
								len += rawlen
							ElseIf rawlen < -1 Then
								offs += ClassDescriptor.Sizeof(-2 - rawlen)
							End If
						Next
					End If
					Exit Select
			End Select
			Return offs
		End Function

		Private Function unpackRawValue(body As Byte(), offs As Integer, ByRef val As Object, recursiveLoading As Boolean) As Integer
			Dim len As Integer = Bytes.unpack4(body, offs)
			offs += 4
			If len >= 0 Then
				Dim ms As New System.IO.MemoryStream(body, offs, len)
				val = objectFormatter.Deserialize(ms)
				ms.Close()
				offs += len
			Else
				Select Case DirectCast(-2 - len, ClassDescriptor.FieldType)
					Case ClassDescriptor.FieldType.tpBoolean
						val = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
						Exit Select
					Case ClassDescriptor.FieldType.tpByte
						val = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
						Exit Select
					Case ClassDescriptor.FieldType.tpSByte
						val = CSByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))
						Exit Select
					Case ClassDescriptor.FieldType.tpChar
						val = CChar(Bytes.unpack2(body, offs))
						offs += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpShort
						val = Bytes.unpack2(body, offs)
						offs += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpUShort
						val = CUShort(Bytes.unpack2(body, offs))
						offs += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpOid
						val = Bytes.unpack4(body, offs)
						offs += 4
						Exit Select
					Case ClassDescriptor.FieldType.tpUInt
						val = CUInt(Bytes.unpack4(body, offs))
						offs += 4
						Exit Select
					Case ClassDescriptor.FieldType.tpLong
						val = Bytes.unpack8(body, offs)
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpULong
						val = CULng(Bytes.unpack8(body, offs))
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpFloat
						val = Bytes.unpackF4(body, offs)
						offs += 4
						Exit Select
					Case ClassDescriptor.FieldType.tpDouble
						val = Bytes.unpackF8(body, offs)
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpDate
						val = Bytes.unpackDate(body, offs)
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpGuid
						val = Bytes.unpackGuid(body, offs)
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpDecimal
						val = Bytes.unpackDecimal(body, offs)
						offs += 8
						Exit Select
					Case ClassDescriptor.FieldType.tpObject
						val = unswizzle(Bytes.unpack4(body, offs), GetType(Persistent), recursiveLoading)
						offs += 4
						Exit Select
					Case Else
						val = Nothing
						Exit Select
				End Select
			End If
			Return offs
		End Function

		Public Function unpackField(body As Byte(), offs As Integer, recursiveLoading As Boolean, ByRef val As Object, fd As ClassDescriptor.FieldDescriptor, type As ClassDescriptor.FieldType, _
			po As IPersistent) As Integer
			Dim len As Integer
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					val = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
					Exit Select

				Case ClassDescriptor.FieldType.tpByte
					val = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
					Exit Select

				Case ClassDescriptor.FieldType.tpSByte
					val = CSByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))
					Exit Select

				Case ClassDescriptor.FieldType.tpChar
					val = CChar(Bytes.unpack2(body, offs))
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpShort
					val = Bytes.unpack2(body, offs)
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpUShort
					val = CUShort(Bytes.unpack2(body, offs))
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpEnum
					val = [Enum].ToObject(fd.field.FieldType, Bytes.unpack4(body, offs))
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpOid
					val = Bytes.unpack4(body, offs)
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpUInt
					val = CUInt(Bytes.unpack4(body, offs))
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpLong
					val = Bytes.unpack8(body, offs)
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpULong
					val = CULng(Bytes.unpack8(body, offs))
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpFloat
					val = Bytes.unpackF4(body, offs)
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpDouble
					val = Bytes.unpackF8(body, offs)
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpDecimal
					val = Bytes.unpackDecimal(body, offs)
					offs += 16
					Exit Select

				Case ClassDescriptor.FieldType.tpGuid
					val = Bytes.unpackGuid(body, offs)
					offs += 16
					Exit Select

				Case ClassDescriptor.FieldType.tpString
					If True Then
						Dim str As String
						offs = Bytes.unpackString(body, offs, str)
						val = str
						Exit Select
					End If

				Case ClassDescriptor.FieldType.tpDate
					val = Bytes.unpackDate(body, offs)
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpObject
					If fd Is Nothing Then
						val = unswizzle(Bytes.unpack4(body, offs), GetType(Persistent), recursiveLoading)
					Else
						val = unswizzle(Bytes.unpack4(body, offs), fd.field.FieldType, fd.recursiveLoading Or recursiveLoading)
					End If
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpValue
					val = fd.field.GetValue(val)
					offs = unpackObject(val, fd.valueDesc, recursiveLoading, body, offs, po)
					Exit Select

				Case ClassDescriptor.FieldType.tpRaw
					offs = unpackRawValue(body, offs, val, recursiveLoading)
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfByte
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Byte() = New Byte(len - 1) {}
						Array.Copy(body, offs, arr, 0, len)
						offs += len
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfSByte
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As SByte() = New SByte(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = CSByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfBoolean
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Boolean() = New Boolean(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfShort
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Short() = New Short(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpack2(body, offs)
							offs += 2
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfUShort
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As UShort() = New UShort(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = CUShort(Bytes.unpack2(body, offs))
							offs += 2
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfChar
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Char() = New Char(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = CChar(Bytes.unpack2(body, offs))
							offs += 2
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfEnum
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim elemType As System.Type = fd.field.FieldType.GetElementType()
						Dim arr As Array = Array.CreateInstance(elemType, len)
						For j As Integer = 0 To len - 1
							arr.SetValue([Enum].ToObject(elemType, Bytes.unpack4(body, offs)), j)
							offs += 4
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfInt
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Integer() = New Integer(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpack4(body, offs)
							offs += 4
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfUInt
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As UInteger() = New UInteger(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = CUInt(Bytes.unpack4(body, offs))
							offs += 4
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfLong
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Long() = New Long(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpack8(body, offs)
							offs += 8
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfULong
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As ULong() = New ULong(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = CULng(Bytes.unpack8(body, offs))
							offs += 8
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfFloat
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Single() = New Single(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpackF4(body, offs)
							offs += 4
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDouble
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Double() = New Double(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpackF8(body, offs)
							offs += 8
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDate
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As System.DateTime() = New System.DateTime(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpackDate(body, offs)
							offs += 8
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfString
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As String() = New String(len - 1) {}
						For j As Integer = 0 To len - 1
							offs = Bytes.unpackString(body, offs, arr(j))
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDecimal
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Decimal() = New Decimal(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpackDecimal(body, offs)
							offs += 16
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfGuid
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Guid() = New Guid(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpackGuid(body, offs)
							offs += 16
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfObject
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim elemType As Type = fd.field.FieldType.GetElementType()
						Dim arr As IPersistent() = DirectCast(Array.CreateInstance(elemType, len), IPersistent())
						For j As Integer = 0 To len - 1
							arr(j) = unswizzle(Bytes.unpack4(body, offs), elemType, recursiveLoading)
							offs += 4
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfValue
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim elemType As Type = fd.field.FieldType.GetElementType()
						Dim arr As Array = Array.CreateInstance(elemType, len)
						Dim valueDesc As ClassDescriptor = fd.valueDesc
						For j As Integer = 0 To len - 1
							Dim elem As Object = arr.GetValue(j)
							offs = unpackObject(elem, valueDesc, recursiveLoading, body, offs, po)
							arr.SetValue(elem, j)
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfRaw
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim elemType As Type = fd.field.FieldType.GetElementType()
						Dim arr As Array = Array.CreateInstance(elemType, len)
						For j As Integer = 0 To len - 1
							Dim elem As Object
							offs = unpackRawValue(body, offs, elem, recursiveLoading)
							arr.SetValue(elem, j)
						Next
						val = arr
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpLink
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As IPersistent() = New IPersistent(len - 1) {}
						For j As Integer = 0 To len - 1
							Dim elemOid As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If elemOid <> 0 Then
								arr(j) = New PersistentStub(Me, elemOid)
							End If
						Next
						val = fd.constructor.Invoke(Me, New Object() {arr, po})
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfOid
					len = Bytes.unpack4(body, offs)
					offs += 4
					If len < 0 Then
						val = Nothing
					Else
						Dim arr As Integer() = New Integer(len - 1) {}
						For j As Integer = 0 To len - 1
							arr(j) = Bytes.unpack4(body, offs)
							offs += 4
						Next
						val = fd.constructor.Invoke(Me, New Object() {arr, po})
					End If
					Exit Select
			End Select
			Return offs
		End Function

		Friend Function packObject(obj As IPersistent) As Byte()
			Dim buf As New ByteBuffer()
			Dim offs As Integer = ObjectHeader.Sizeof
			buf.extend(offs)
			Dim desc As ClassDescriptor = getClassDescriptor(obj.[GetType]())
			If desc.serializer IsNot Nothing Then
				offs = desc.serializer.pack(Me, obj, buf)
			Else
				offs = packObject(obj, desc, offs, buf, obj)
			End If
			ObjectHeader.setSize(buf.arr, 0, offs)
			ObjectHeader.setType(buf.arr, 0, desc.Oid)
			Return buf.arr
		End Function

		Public Function packObject(obj As Object, desc As ClassDescriptor, offs As Integer, buf As ByteBuffer, po As IPersistent) As Integer
			Dim flds As ClassDescriptor.FieldDescriptor() = desc.allFields

			Dim i As Integer = 0, n As Integer = flds.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = flds(i)
				offs = packField(buf, offs, fd.field.GetValue(obj), fd, fd.type, po)
				i += 1
			End While
			Return offs
		End Function

		Public Function packRawValue(buf As ByteBuffer, offs As Integer, val As Object) As Integer
			If val Is Nothing Then
				buf.extend(offs + 4)
				Bytes.pack4(buf.arr, offs, -1)
				offs += 4
			ElseIf TypeOf val Is IPersistent Then
				buf.extend(offs + 8)
				Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpObject))
				Bytes.pack4(buf.arr, offs + 4, swizzle(DirectCast(val, IPersistent)))
				offs += 8
			Else
				Dim t As Type = val.[GetType]()
				If t Is GetType(Boolean) Then
					buf.extend(offs + 5)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpBoolean))
					buf.arr(offs + 4) = CByte(If(CBool(val), 1, 0))
					offs += 5
				ElseIf t Is GetType(Char) Then
					buf.extend(offs + 6)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpChar))
					Bytes.pack2(buf.arr, offs + 4, CShort(AscW(CChar(val))))
					offs += 6
				ElseIf t Is GetType(Byte) Then
					buf.extend(offs + 5)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpByte))
					buf.arr(offs + 4) = CByte(val)
					offs += 5
				ElseIf t Is GetType(SByte) Then
					buf.extend(offs + 5)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpSByte))
					buf.arr(offs + 4) = CByte(CSByte(val))
					offs += 5
				ElseIf t Is GetType(Short) Then
					buf.extend(offs + 6)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpShort))
					Bytes.pack2(buf.arr, offs + 4, CShort(val))
					offs += 6
				ElseIf t Is GetType(UShort) Then
					buf.extend(offs + 6)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpUShort))
					Bytes.pack2(buf.arr, offs + 4, CShort(CUShort(val)))
					offs += 6
				ElseIf t Is GetType(Integer) Then
					buf.extend(offs + 8)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpInt))
					Bytes.pack4(buf.arr, offs + 4, CInt(val))
					offs += 8
				ElseIf t Is GetType(UInteger) Then
					buf.extend(offs + 8)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpUInt))
					Bytes.pack4(buf.arr, offs + 4, CInt(CUInt(val)))
					offs += 8
				ElseIf t Is GetType(Long) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpLong))
					Bytes.pack8(buf.arr, offs + 4, CLng(val))
					offs += 12
				ElseIf t Is GetType(ULong) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpULong))
					Bytes.pack8(buf.arr, offs + 4, CLng(CULng(val)))
					offs += 12
				ElseIf t Is GetType(Single) Then
					buf.extend(offs + 8)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpFloat))
					Bytes.packF4(buf.arr, offs + 4, CSng(val))
					offs += 8
				ElseIf t Is GetType(Double) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpDouble))
					Bytes.packF8(buf.arr, offs + 4, CDbl(val))
					offs += 12
				ElseIf t Is GetType(DateTime) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpDate))
					Bytes.packDate(buf.arr, offs + 4, CType(val, DateTime))
					offs += 12
				ElseIf t Is GetType(Guid) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpGuid))
					Bytes.packGuid(buf.arr, offs + 4, CType(val, Guid))
					offs += 12
				ElseIf t Is GetType([Decimal]) Then
					buf.extend(offs + 12)
					Bytes.pack4(buf.arr, offs, -2 - CInt(ClassDescriptor.FieldType.tpDecimal))
					Bytes.packDecimal(buf.arr, offs + 4, CDec(val))
					offs += 12
				Else
					Dim ms As New System.IO.MemoryStream()
					objectFormatter.Serialize(ms, val)
					ms.Close()
					Dim arr As Byte() = ms.ToArray()
					Dim len As Integer = arr.Length
					buf.extend(offs + 4 + len)
					Bytes.pack4(buf.arr, offs, len)
					offs += 4
					Array.Copy(arr, 0, buf.arr, offs, len)
					offs += len
				End If
			End If
			Return offs
		End Function

		Public Function packField(buf As ByteBuffer, offs As Integer, val As Object, fd As ClassDescriptor.FieldDescriptor, type As ClassDescriptor.FieldType, po As IPersistent) As Integer
			Select Case type
				Case ClassDescriptor.FieldType.tpByte
					Return buf.packI1(offs, CByte(val))
				Case ClassDescriptor.FieldType.tpSByte
					Return buf.packI1(offs, CSByte(val))
				Case ClassDescriptor.FieldType.tpBoolean
					Return buf.packBool(offs, CBool(val))
				Case ClassDescriptor.FieldType.tpShort
					Return buf.packI2(offs, CShort(val))
				Case ClassDescriptor.FieldType.tpUShort
					Return buf.packI2(offs, CUShort(val))
				Case ClassDescriptor.FieldType.tpChar
					Return buf.packI2(offs, CChar(val))
				Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpOid
					Return buf.packI4(offs, CInt(val))
				Case ClassDescriptor.FieldType.tpUInt
					Return buf.packI4(offs, CInt(CUInt(val)))
				Case ClassDescriptor.FieldType.tpLong
					Return buf.packI8(offs, CLng(val))
				Case ClassDescriptor.FieldType.tpULong
					Return buf.packI8(offs, CLng(CULng(val)))
				Case ClassDescriptor.FieldType.tpFloat
					Return buf.packF4(offs, CSng(val))
				Case ClassDescriptor.FieldType.tpDouble
					Return buf.packF8(offs, CDbl(val))
				Case ClassDescriptor.FieldType.tpDecimal
					Return buf.packDecimal(offs, CDec(val))
				Case ClassDescriptor.FieldType.tpGuid
					Return buf.packGuid(offs, CType(val, Guid))
				Case ClassDescriptor.FieldType.tpDate
					Return buf.packDate(offs, CType(val, DateTime))
				Case ClassDescriptor.FieldType.tpString
					Return buf.packString(offs, DirectCast(val, String))
				Case ClassDescriptor.FieldType.tpValue
					Return packObject(val, fd.valueDesc, offs, buf, po)
				Case ClassDescriptor.FieldType.tpObject
					Return buf.packI4(offs, swizzle(DirectCast(val, IPersistent)))
				Case ClassDescriptor.FieldType.tpRaw
					offs = packRawValue(buf, offs, val)
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfByte
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Byte() = DirectCast(val, Byte())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						Array.Copy(arr, 0, buf.arr, offs, len)
						offs += len
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfSByte
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As SByte() = DirectCast(val, SByte())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						Dim j As Integer = 0
						While j < len
							buf.arr(offs) = CByte(arr(j))
							j += 1
							offs += 1
						End While
						offs += len
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfBoolean
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Boolean() = DirectCast(val, Boolean())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						Dim j As Integer = 0
						While j < len
							buf.arr(offs) = CByte(If(arr(j), 1, 0))
							j += 1
							offs += 1
						End While
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfShort
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Short() = DirectCast(val, Short())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 2)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack2(buf.arr, offs, arr(j))
							offs += 2
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfUShort
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As UShort() = DirectCast(val, UShort())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 2)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack2(buf.arr, offs, CShort(arr(j)))
							offs += 2
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfChar
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Char() = DirectCast(val, Char())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 2)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack2(buf.arr, offs, CShort(AscW(arr(j))))
							offs += 2
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfEnum
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Array = DirectCast(val, Array)
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, CInt(arr.GetValue(j)))
							offs += 4
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfInt
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Integer() = DirectCast(val, Integer())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, arr(j))
							offs += 4
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfUInt
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As UInteger() = DirectCast(val, UInteger())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, CInt(arr(j)))
							offs += 4
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfLong
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Long() = DirectCast(val, Long())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 8)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack8(buf.arr, offs, arr(j))
							offs += 8
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfULong
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As ULong() = DirectCast(val, ULong())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 8)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack8(buf.arr, offs, CLng(arr(j)))
							offs += 8
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfFloat
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Single() = DirectCast(val, Single())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.packF4(buf.arr, offs, arr(j))
							offs += 4
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDouble
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Double() = DirectCast(val, Double())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 8)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.packF8(buf.arr, offs, arr(j))
							offs += 8
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfValue
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Array = DirectCast(val, Array)
						Dim len As Integer = arr.Length
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						Dim elemDesc As ClassDescriptor = fd.valueDesc
						For j As Integer = 0 To len - 1
							offs = packObject(arr.GetValue(j), elemDesc, offs, buf, po)
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDate
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As DateTime() = DirectCast(val, DateTime())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 8)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.packDate(buf.arr, offs, arr(j))
							offs += 8
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfDecimal
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Decimal() = DirectCast(val, Decimal())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 16)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.packDecimal(buf.arr, offs, arr(j))
							offs += 16
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfGuid
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Guid() = DirectCast(val, Guid())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 16)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.packGuid(buf.arr, offs, arr(j))
							offs += 16
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfString
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As String() = DirectCast(val, String())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							offs = buf.packString(offs, arr(j))
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfObject
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As IPersistent() = DirectCast(val, IPersistent())
						Dim len As Integer = arr.Length
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, swizzle(arr(j)))
							offs += 4
						Next
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfRaw
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As Array = DirectCast(val, Array)
						Dim len As Integer = arr.Length
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							offs = packRawValue(buf, offs, arr.GetValue(j))
						Next
					End If
					Exit Select
				Case ClassDescriptor.FieldType.tpLink
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim link As IGenericLink = DirectCast(val, IGenericLink)
						link.SetOwner(po)
						Dim len As Integer = link.Size()
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, swizzle(link.GetRaw(j)))
							offs += 4
						Next
						link.Unpin()
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfOid
					If val Is Nothing Then
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
					Else
						Dim arr As IGenericPArray = DirectCast(val, IGenericPArray)
						arr.SetOwner(po)
						Dim len As Integer = arr.Size()
						buf.extend(offs + 4 + len * 4)
						Bytes.pack4(buf.arr, offs, len)
						offs += 4
						For j As Integer = 0 To len - 1
							Bytes.pack4(buf.arr, offs, arr.GetOid(j))
							offs += 4
						Next
					End If
					Exit Select
			End Select
			Return offs
		End Function

		Public Property Loader() As IClassLoader

			Get
				Return m_loader
			End Get
			Set
				m_loader = value
			End Set
		End Property

		Private initIndexSize As Integer = dbDefaultInitIndexSize
		Private m_objectCacheInitSize As Integer = dbDefaultObjectCacheInitSize
		Private m_extensionQuantum As Long = dbDefaultExtensionQuantum
		Private m_cacheKind As CacheType = CacheType.Lru
		#If WITH_OLD_BTREE Then
		Private m_alternativeBtree As Boolean = False
		#End If
		Private m_backgroundGc As Boolean = False

		#If WITH_REPLICATION Then
		Friend m_replicationAck As Boolean = False
		#End If

		Friend pool As PagePool
		Friend header As Header
		' base address of database file mapping
		Friend dirtyPagesMap As Integer()
		' bitmap of changed pages in current index
		Friend modified As Boolean

		Friend currRBitmapPage As Integer
		'current bitmap page for allocating records
		Friend currRBitmapOffs As Integer
		'offset in current bitmap page for allocating 
		'unaligned records
		Friend currPBitmapPage As Integer
		'current bitmap page for allocating page objects
		Friend currPBitmapOffs As Integer
		'offset in current bitmap page for allocating 
		'page objects
		Friend reservedChain As Location

		Friend committedIndexSize As Integer
		Friend currIndexSize As Integer

		Friend enableCodeGeneration As Boolean = True

		#If CF Then
		Friend Shared assemblies As ArrayList
		Private transactionMonitor As CNetMonitor
		#Else
		Friend codeGenerationThread As Thread
		Private transactionMonitor As Object
		Private wrapperHash As New Dictionary(Of Type, Type)()
		#End If
		Private nNestedTransactions As Integer
		Private nBlockedTransactions As Integer
		Private nCommittedTransactions As Integer
		Private scheduledCommitTime As Long
		Private transactionLock As PersistentResource

		Friend objectFormatter As System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
		Friend currIndex As Integer
		' copy of header.root, used to allow read access to the database 
		' during transaction commit
		Friend m_usedSize As Long
		' total size of allocated objects since the beginning of the session
		Friend bitmapPageAvailableSpace As Integer()
		Friend opened As Boolean

		Friend greyBitmap As Integer()
		' bitmap of objects visited but not yet marked during GC
		Friend blackBitmap As Integer()
		' bitmap of objects marked during GC 
		Friend m_gcThreshold As Long
		Friend allocatedDelta As Long
		Friend gcDone As Boolean
		Friend gcActive As Boolean
		Friend gcGo As Boolean
		Friend backgroundGcMonitor As Object
		Friend backgroundGcStartMonitor As Object
		Friend gcThread As Thread

		Private m_loader As IClassLoader

		Friend resolvedTypes As Dictionary(Of String, Type)

		Friend objectCache As OidHashTable
		Friend classDescMap As Dictionary(Of Type, ClassDescriptor)
		Friend descList As ClassDescriptor

		Friend Shared ReadOnly m_transactionContext As LocalDataStoreSlot = Thread.AllocateDataSlot()
		Friend useSerializableTransactions As Boolean
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class

	Class RootPage
		Friend size As Long
		' database file size
		Friend index As Long
		' offset to object index
		Friend shadowIndex As Long
		' offset to shadow index
		Friend usedSize As Long
		' size used by objects
		Friend indexSize As Integer
		' size of object index
		Friend shadowIndexSize As Integer
		' size of object index
		Friend indexUsed As Integer
		' used part of the index   
		Friend freeList As Integer
		' L1 list of free descriptors
		Friend bitmapEnd As Integer
		' index of last allocated bitmap page
		Friend rootObject As Integer
		' oid of root object
		Friend classDescList As Integer
		' List of class descriptors
		Friend bitmapExtent As Integer
		' Allocation bitmap offset and size
		Friend Const Sizeof As Integer = 64
	End Class

	Class Header
		Friend curr As Integer
		' current root
		Friend dirty As Boolean
		' database was not closed normally
		Friend initialized As Boolean
		' database is initilaized
		Friend root As RootPage()

		Friend Shared Sizeof As Integer = 3 + RootPage.Sizeof * 2

		Friend Sub pack(rec As Byte())
			Dim offs As Integer = 0
			rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(curr)
			rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(If(dirty, 1, 0))
			rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(If(initialized, 1, 0))
			For i As Integer = 0 To 1
				Bytes.pack8(rec, offs, root(i).size)
				offs += 8
				Bytes.pack8(rec, offs, root(i).index)
				offs += 8
				Bytes.pack8(rec, offs, root(i).shadowIndex)
				offs += 8
				Bytes.pack8(rec, offs, root(i).usedSize)
				offs += 8
				Bytes.pack4(rec, offs, root(i).indexSize)
				offs += 4
				Bytes.pack4(rec, offs, root(i).shadowIndexSize)
				offs += 4
				Bytes.pack4(rec, offs, root(i).indexUsed)
				offs += 4
				Bytes.pack4(rec, offs, root(i).freeList)
				offs += 4
				Bytes.pack4(rec, offs, root(i).bitmapEnd)
				offs += 4
				Bytes.pack4(rec, offs, root(i).rootObject)
				offs += 4
				Bytes.pack4(rec, offs, root(i).classDescList)
				offs += 4
				Bytes.pack4(rec, offs, root(i).bitmapExtent)
				offs += 4
			Next
		End Sub

		Friend Sub unpack(rec As Byte())
			Dim offs As Integer = 0
			curr = rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
			dirty = rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
			initialized = rec(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
			root = New RootPage(1) {}
			For i As Integer = 0 To 1
				root(i) = New RootPage()
				root(i).size = Bytes.unpack8(rec, offs)
				offs += 8
				root(i).index = Bytes.unpack8(rec, offs)
				offs += 8
				root(i).shadowIndex = Bytes.unpack8(rec, offs)
				offs += 8
				root(i).usedSize = Bytes.unpack8(rec, offs)
				offs += 8
				root(i).indexSize = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).shadowIndexSize = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).indexUsed = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).freeList = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).bitmapEnd = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).rootObject = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).classDescList = Bytes.unpack4(rec, offs)
				offs += 4
				root(i).bitmapExtent = Bytes.unpack4(rec, offs)
				offs += 4
			Next
		End Sub
	End Class
End Namespace
