Imports Volante
Imports System.Diagnostics
Namespace Volante.Impl

	Class PagePool
		Friend lru As LRU
		Friend freePages As Page
		Friend hashTable As Page()
		Friend poolSize As Integer
		Friend autoExtended As Boolean
		Friend file As IFile

		Friend nDirtyPages As Integer
		Friend dirtyPages As Page()

		Friend flushing As Boolean

		Const INFINITE_POOL_INITIAL_SIZE As Integer = 8

		Friend Sub New(poolSize As Integer)
			If poolSize = 0 Then
				autoExtended = True
				poolSize = INFINITE_POOL_INITIAL_SIZE
			End If
			Me.poolSize = poolSize
		End Sub

		Friend Function find(addr As Long, state As Integer) As Page
			Debug.Assert((addr And (Page.pageSize - 1)) = 0)
			Dim pg As Page
			Dim pageNo As Integer = CInt(CULng(addr) >> Page.pageBits)
			Dim hashCode As Integer = pageNo Mod poolSize

			SyncLock Me
				Dim nCollisions As Integer = 0
				pg = hashTable(hashCode)
				While pg IsNot Nothing
					If pg.offs = addr Then
						If System.Math.Max(System.Threading.Interlocked.Increment(pg.accessCount),pg.accessCount - 1) = 0 Then
							pg.unlink()
						End If
						Exit While
					End If
					nCollisions += 1
					pg = pg.collisionChain
				End While
				If pg Is Nothing Then
					pg = freePages
					If pg IsNot Nothing Then
						freePages = DirectCast(pg.[next], Page)
					ElseIf autoExtended Then
						If pageNo >= poolSize Then
							Dim newPoolSize As Integer = If(pageNo >= poolSize * 2, pageNo + 1, poolSize * 2)
							Dim newHashTable As Page() = New Page(newPoolSize - 1) {}
							Array.Copy(hashTable, 0, newHashTable, 0, hashTable.Length)
							hashTable = newHashTable
							poolSize = newPoolSize
						End If
						pg = New Page()
						hashCode = pageNo
					Else
                        Debug.Assert(lru.prev IsNot lru, "unfixed page available")
						pg = DirectCast(lru.prev, Page)
						pg.unlink()
						SyncLock pg
							If (pg.state And Page.psDirty) <> 0 Then
								pg.state = 0
								file.Write(pg.offs, pg.data)
								If Not flushing Then
									dirtyPages(pg.writeQueueIndex) = dirtyPages(System.Threading.Interlocked.Decrement(nDirtyPages))
									dirtyPages(pg.writeQueueIndex).writeQueueIndex = pg.writeQueueIndex
								End If
							End If
						End SyncLock
						Dim h As Integer = CInt(pg.offs >> Page.pageBits) Mod poolSize
						Dim curr As Page = hashTable(h), prev As Page = Nothing
                        While curr IsNot pg
                            prev = curr
                            curr = curr.collisionChain
                        End While

						If prev Is Nothing Then
							hashTable(h) = pg.collisionChain
						Else
							prev.collisionChain = pg.collisionChain
						End If
					End If
					pg.accessCount = 1
					pg.offs = addr
					pg.state = Page.psRaw
					pg.collisionChain = hashTable(hashCode)
					hashTable(hashCode) = pg
				End If
				If (pg.state And Page.psDirty) = 0 AndAlso (state And Page.psDirty) <> 0 Then
					Debug.Assert(Not flushing)
					If nDirtyPages >= dirtyPages.Length Then
						Dim newDirtyPages As Page() = New Page(nDirtyPages * 2 - 1) {}
						Array.Copy(dirtyPages, 0, newDirtyPages, 0, dirtyPages.Length)
						dirtyPages = newDirtyPages
					End If
					dirtyPages(nDirtyPages) = pg
					pg.writeQueueIndex = System.Math.Max(System.Threading.Interlocked.Increment(nDirtyPages),nDirtyPages - 1)
					pg.state = pg.state Or Page.psDirty
				End If

				If (pg.state And Page.psRaw) <> 0 Then
					If file.Read(pg.offs, pg.data) < Page.pageSize Then
						Array.Clear(pg.data, 0, Page.pageSize)
					End If

					pg.state = pg.state And Not Page.psRaw
				End If
			End SyncLock
			Return pg
		End Function

		Friend Sub copy(dst As Long, src As Long, size As Long)
			Dim dstOffs As Integer = CInt(dst) And (Page.pageSize - 1)
			Dim srcOffs As Integer = CInt(src) And (Page.pageSize - 1)
			dst -= dstOffs
			src -= srcOffs
			Dim dstPage As Page = find(dst, Page.psDirty)
			Dim srcPage As Page = find(src, 0)
			Do
				If dstOffs = Page.pageSize Then
					unfix(dstPage)
					dst += Page.pageSize
					dstPage = find(dst, Page.psDirty)
					dstOffs = 0
				End If
				If srcOffs = Page.pageSize Then
					unfix(srcPage)
					src += Page.pageSize
					srcPage = find(src, 0)
					srcOffs = 0
				End If
				Dim len As Long = size
				If len > Page.pageSize - srcOffs Then
					len = Page.pageSize - srcOffs
				End If

				If len > Page.pageSize - dstOffs Then
					len = Page.pageSize - dstOffs
				End If

				Array.Copy(srcPage.data, srcOffs, dstPage.data, dstOffs, CInt(len))
				srcOffs = CInt(srcOffs + len)
				dstOffs = CInt(dstOffs + len)
				size -= len
			Loop While size <> 0
			unfix(dstPage)
			unfix(srcPage)
		End Sub

		Friend Sub write(dstPos As Long, src As Byte())
			Debug.Assert((dstPos And (Page.pageSize - 1)) = 0)
			Debug.Assert((src.Length And (Page.pageSize - 1)) = 0)
			Dim i As Integer = 0
			While i < src.Length
				Dim pg As Page = find(dstPos, Page.psDirty)
				Dim dst As Byte() = pg.data
				For j As Integer = 0 To Page.pageSize - 1
					dst(j) = src(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1))
				Next
				unfix(pg)
				dstPos += Page.pageSize
			End While
		End Sub

		Friend Sub open(f As IFile)
			file = f
			hashTable = New Page(poolSize - 1) {}
			dirtyPages = New Page(poolSize - 1) {}
			nDirtyPages = 0
			lru = New LRU()
			freePages = Nothing
			If autoExtended Then
				Return
			End If

			Dim i As Integer = poolSize
			While System.Threading.Interlocked.Decrement(i) >= 0
				Dim pg As New Page()
				pg.[next] = freePages
				freePages = pg
			End While
		End Sub

		Friend Sub close()
			SyncLock Me
				file.Close()
				hashTable = Nothing
				dirtyPages = Nothing
				lru = Nothing
				freePages = Nothing
			End SyncLock
		End Sub

		Friend Sub unfix(pg As Page)
			SyncLock Me
				Debug.Assert(pg.accessCount > 0)
				If System.Threading.Interlocked.Decrement(pg.accessCount) = 0 Then
					lru.link(pg)
				End If
			End SyncLock
		End Sub

		Friend Sub modify(pg As Page)
			SyncLock Me
				Debug.Assert(pg.accessCount > 0)
				If (pg.state And Page.psDirty) = 0 Then
					Debug.Assert(Not flushing)
					pg.state = pg.state Or Page.psDirty
					If nDirtyPages >= dirtyPages.Length Then
						Dim newDirtyPages As Page() = New Page(nDirtyPages * 2 - 1) {}
						Array.Copy(dirtyPages, 0, newDirtyPages, 0, dirtyPages.Length)
						dirtyPages = newDirtyPages
					End If
					dirtyPages(nDirtyPages) = pg
					pg.writeQueueIndex = System.Math.Max(System.Threading.Interlocked.Increment(nDirtyPages),nDirtyPages - 1)
				End If
			End SyncLock
		End Sub

		Friend Function getPage(addr As Long) As Page
			Return find(addr, 0)
		End Function

		Friend Function putPage(addr As Long) As Page
			Return find(addr, Page.psDirty)
		End Function

		Friend Function [get](pos As Long) As Byte()
			Debug.Assert(pos <> 0)
			Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
			Dim pg As Page = find(pos - offs, 0)
			Dim size As Integer = ObjectHeader.getSize(pg.data, offs)
			Debug.Assert(size >= ObjectHeader.Sizeof)
			Dim obj As Byte() = New Byte(size - 1) {}
			Dim dst As Integer = 0
			While size > Page.pageSize - offs
				Array.Copy(pg.data, offs, obj, dst, Page.pageSize - offs)
				unfix(pg)
				size -= Page.pageSize - offs
				pos += Page.pageSize - offs
				dst += Page.pageSize - offs
				pg = find(pos, 0)
				offs = 0
			End While
			Array.Copy(pg.data, offs, obj, dst, size)
			unfix(pg)
			Return obj
		End Function

		Friend Sub put(pos As Long, obj As Byte())
			put(pos, obj, obj.Length)
		End Sub

		Friend Sub put(pos As Long, obj As Byte(), size As Integer)
			Dim offs As Integer = CInt(pos) And (Page.pageSize - 1)
			Dim pg As Page = find(pos - offs, Page.psDirty)
			Dim src As Integer = 0
			While size > Page.pageSize - offs
				Array.Copy(obj, src, pg.data, offs, Page.pageSize - offs)
				unfix(pg)
				size -= Page.pageSize - offs
				pos += Page.pageSize - offs
				src += Page.pageSize - offs
				pg = find(pos, Page.psDirty)
				offs = 0
			End While
			Array.Copy(obj, src, pg.data, offs, size)
			unfix(pg)
		End Sub

		#If CF Then
		Private Class PageComparator
			Implements System.Collections.IComparer
			Public Function Compare(o1 As Object, o2 As Object) As Integer Implements System.Collections.IComparer.Compare
				Dim delta As Long = DirectCast(o1, Page).offs - DirectCast(o2, Page).offs
				Return If(delta < 0, -1, If(delta = 0, 0, 1))
			End Function
		End Class
		Shared pageComparator As New PageComparator()
		#End If

		Friend Overridable Sub flush()
			SyncLock Me
				flushing = True
				#If CF Then
				Array.Sort(dirtyPages, 0, nDirtyPages, pageComparator)
				#Else
					#End If
				Array.Sort(dirtyPages, 0, nDirtyPages)
			End SyncLock
			For i As Integer = 0 To nDirtyPages - 1
				Dim pg As Page = dirtyPages(i)
				SyncLock pg
					If (pg.state And Page.psDirty) <> 0 Then
						file.Write(pg.offs, pg.data)
						pg.state = pg.state And Not Page.psDirty
					End If
				End SyncLock
			Next
			file.Sync()
			nDirtyPages = 0
			flushing = False
		End Sub
	End Class
End Namespace
