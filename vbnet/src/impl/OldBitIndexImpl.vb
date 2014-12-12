#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Class OldBitIndexImpl(Of T As {Class, IPersistent})
		Inherits OldBtree(Of IPersistent, T)
		Implements IBitIndex(Of T)
		Private Class Key
			Friend key As Integer
			Friend oid As Integer

			Friend Sub New(key__1 As Integer, oid As Integer)
				Me.key = key__1
				Me.oid = oid
			End Sub
		End Class

		Friend Sub New()
			MyBase.New(ClassDescriptor.FieldType.tpInt, True)
		End Sub

		Public Default Property Item(obj As T) As Integer
			Get
				Return [Get](obj)
			End Get
			Set
				Put(obj, value)
			End Set
		End Property

		Public Function [Get](obj As T) As Integer
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			If root = 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If
			Return BitIndexPage.find(db, root, obj.Oid, height)
		End Function

		Public Sub Put(obj As T, mask As Integer)
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			If db Is Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
			End If
			If Not obj.IsPersistent() Then
				db.MakePersistent(obj)
			End If
			Dim ins As New Key(mask, obj.Oid)
			If root = 0 Then
				root = BitIndexPage.allocate(db, 0, ins)
				height = 1
			Else
				Dim result As OldBtreeResult = BitIndexPage.insert(db, root, ins, height)
				If result = OldBtreeResult.Overflow Then
					root = BitIndexPage.allocate(db, root, ins)
					height += 1
				End If
			End If
			updateCounter += 1
			nElems += 1
			Modify()
		End Sub

		Public Overrides Function Remove(obj As T) As Boolean
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			If db Is Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
			End If
			If root = 0 Then
				Return False
			End If
			Dim result As OldBtreeResult = BitIndexPage.remove(db, root, obj.Oid, height)
			If result = OldBtreeResult.NotFound Then
				Return False
			End If
			nElems -= 1
			If result = OldBtreeResult.Underflow Then
				Dim pg As Page = db.getPage(root)
				If BitIndexPage.getnItems(pg) = 0 Then
					Dim newRoot As Integer = 0
					If height <> 1 Then
						newRoot = BitIndexPage.getItem(pg, BitIndexPage.maxItems - 1)
					End If
					db.freePage(root)
					root = newRoot
					height -= 1
				End If
				db.pool.unfix(pg)
			End If
			updateCounter += 1
			Modify()
			Return True
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator(0, 0)
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of T)
			Return GetEnumerator(0, 0)
		End Function

		Public Function GetEnumerator(setBits As Integer, clearBits As Integer) As IEnumerator(Of T)
			Return New BitIndexIterator(Me, setBits, clearBits)
		End Function

		Public Function [Select](setBits As Integer, clearBits As Integer) As IEnumerable(Of T)
			Return New BitIndexIterator(Me, setBits, clearBits)
		End Function

		Private Class BitIndexIterator
			Implements IEnumerator(Of T)
			Implements IEnumerable(Of T)
			Friend Sub New(index As OldBitIndexImpl(Of T), setBits As Integer, clearBits As Integer)
				sp = 0
				counter = index.updateCounter
				Dim h As Integer = index.height
				If h = 0 Then
					Return
				End If
				db = DirectCast(index.Database, DatabaseImpl)
				If db Is Nothing Then
					Throw New DatabaseException(DatabaseException.ErrorCode.DELETED_OBJECT)
				End If
				Me.index = index
				Me.setBits = setBits
				Me.clearBits = clearBits

				pageStack = New Integer(h - 1) {}
				posStack = New Integer(h - 1) {}

				Reset()
			End Sub

			Public Sub Reset() Implements IEnumerator.Reset
				sp = 0
				Dim h As Integer = index.height
				Dim pageId As Integer = index.root
				While System.Threading.Interlocked.Decrement(h) >= 0
					pageStack(sp) = pageId
					posStack(sp) = 0
					Dim pg As Page = db.getPage(pageId)
					sp += 1
					pageId = BitIndexPage.getItem(pg, BitIndexPage.maxItems - 1)
					db.pool.unfix(pg)
				End While
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return Me
			End Function

			Public Overridable ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If sp = 0 Then
						Throw New InvalidOperationException()
					End If

					Dim pos As Integer = posStack(sp - 1)
					Dim pg As Page = db.getPage(pageStack(sp - 1))
					Dim curr As IPersistent = db.lookupObject(BitIndexPage.getItem(pg, BitIndexPage.maxItems - pos), Nothing)
					db.pool.unfix(pg)
					Return DirectCast(curr, T)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If counter <> index.updateCounter Then
					Throw New InvalidOperationException("B-Tree was modified")
				End If
				If sp = 0 Then
					Return False
				End If
				Dim pos As Integer = posStack(sp - 1)
				Dim pg As Page = db.getPage(pageStack(sp - 1))
				Do
					Dim [end] As Integer = BitIndexPage.getnItems(pg)

					While pos < [end]
						Dim mask As Integer = BitIndexPage.getItem(pg, pos)
						pos += 1
						If (setBits And mask) = setBits AndAlso (clearBits And mask) = 0 Then
							posStack(sp - 1) = pos
							db.pool.unfix(pg)
							Return True
						End If
					End While

					While System.Threading.Interlocked.Decrement(sp) <> 0
						db.pool.unfix(pg)
						pos = posStack(sp - 1)
						pg = db.getPage(pageStack(sp - 1))
						If System.Threading.Interlocked.Increment(pos) <= BitIndexPage.getnItems(pg) Then
							posStack(sp - 1) = pos
							Do
								Dim pageId As Integer = BitIndexPage.getItem(pg, BitIndexPage.maxItems - 1 - pos)
								db.pool.unfix(pg)
								pg = db.getPage(pageId)
								pageStack(sp) = pageId
								posStack(sp) = InlineAssignHelper(pos, 0)
							Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
							Exit While
						End If
					End While
				Loop While sp <> 0

				db.pool.unfix(pg)
				Return False
			End Function

			Private index As OldBitIndexImpl(Of T)
			Private db As DatabaseImpl
			Private pageStack As Integer()
			Private posStack As Integer()
			Private sp As Integer
			Private setBits As Integer
			Private clearBits As Integer
			Private counter As Integer
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Private Class BitIndexPage
			Inherits OldBtreePage
			Const max As Integer = keySpace / 8

			Friend Shared Function getItem(pg As Page, index As Integer) As Integer
				Return Bytes.unpack4(pg.data, firstKeyOffs + index * 4)
			End Function

			Friend Shared Sub setItem(pg As Page, index As Integer, mask As Integer)
				Bytes.pack4(pg.data, firstKeyOffs + index * 4, mask)
			End Sub

			Friend Shared Function allocate(db As DatabaseImpl, root As Integer, ins As Key) As Integer
				Dim pageId As Integer = db.allocatePage()
				Dim pg As Page = db.putPage(pageId)
				setnItems(pg, 1)
				setItem(pg, 0, ins.key)
				setItem(pg, maxItems - 1, ins.oid)
				setItem(pg, maxItems - 2, root)
				db.pool.unfix(pg)
				Return pageId
			End Function

			Private Shared Sub memcpy(dst_pg As Page, dst_idx As Integer, src_pg As Page, src_idx As Integer, len As Integer)
				Array.Copy(src_pg.data, firstKeyOffs + src_idx * 4, dst_pg.data, firstKeyOffs + dst_idx * 4, len * 4)
			End Sub

			Friend Shared Function find(db As DatabaseImpl, pageId As Integer, oid As Integer, height As Integer) As Integer
				Dim pg As Page = db.getPage(pageId)
				Try
					Dim i As Integer, n As Integer = getnItems(pg), l As Integer = 0, r As Integer = n
					If System.Threading.Interlocked.Decrement(height) = 0 Then
						While l < r
							i = (l + r) >> 1
							If oid > getItem(pg, maxItems - 1 - i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						If r < n AndAlso getItem(pg, maxItems - r - 1) = oid Then
							Return getItem(pg, r)
						End If
						Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
					Else
						While l < r
							i = (l + r) >> 1
							If oid > getItem(pg, i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Return find(db, getItem(pg, maxItems - r - 1), oid, height)
					End If
				Finally
					If pg IsNot Nothing Then
						db.pool.unfix(pg)
					End If
				End Try
			End Function

			Friend Shared Function insert(db As DatabaseImpl, pageId As Integer, ins As Key, height As Integer) As OldBtreeResult
				Dim pg As Page = db.getPage(pageId)
				Dim l As Integer = 0, n As Integer = getnItems(pg), r As Integer = n
				Dim oid As Integer = ins.oid
				Try
					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						While l < r
							Dim i As Integer = (l + r) >> 1
							If oid > getItem(pg, i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(l = r)
						' insert before e[r] 

						Dim result As OldBtreeResult = insert(db, getItem(pg, maxItems - r - 1), ins, height)
						Debug.Assert(result <> OldBtreeResult.NotFound)
						If result <> OldBtreeResult.Overflow Then
							Return result
						End If
						n += 1
					Else
						While l < r
							Dim i As Integer = (l + r) >> 1
							If oid > getItem(pg, maxItems - 1 - i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						If r < n AndAlso oid = getItem(pg, maxItems - 1 - r) Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							setItem(pg, r, ins.key)
							Return OldBtreeResult.Overwrite
						End If
					End If
					db.pool.unfix(pg)
					pg = Nothing
					pg = db.putPage(pageId)
					If n < max Then
						memcpy(pg, r + 1, pg, r, n - r)
						memcpy(pg, maxItems - n - 1, pg, maxItems - n, n - r)
						setItem(pg, r, ins.key)
						setItem(pg, maxItems - 1 - r, ins.oid)
						setnItems(pg, getnItems(pg) + 1)
						Return OldBtreeResult.Done
					Else
						' page is full then divide page 
						pageId = db.allocatePage()
						Dim b As Page = db.putPage(pageId)
						Debug.Assert(n = max)
						Dim m As Integer = max \ 2
						If r < m Then
							memcpy(b, 0, pg, 0, r)
							memcpy(b, r + 1, pg, r, m - r - 1)
							memcpy(pg, 0, pg, m - 1, max - m + 1)
							memcpy(b, maxItems - r, pg, maxItems - r, r)
							setItem(b, r, ins.key)
							setItem(b, maxItems - 1 - r, ins.oid)
							memcpy(b, maxItems - m, pg, maxItems - m + 1, m - r - 1)
							memcpy(pg, maxItems - max + m - 1, pg, maxItems - max, max - m + 1)
						Else
							memcpy(b, 0, pg, 0, m)
							memcpy(pg, 0, pg, m, r - m)
							memcpy(pg, r - m + 1, pg, r, max - r)
							memcpy(b, maxItems - m, pg, maxItems - m, m)
							memcpy(pg, maxItems - r + m, pg, maxItems - r, r - m)
							setItem(pg, r - m, ins.key)
							setItem(pg, maxItems - 1 - r + m, ins.oid)
							memcpy(pg, maxItems - max + m - 1, pg, maxItems - max, max - r)
						End If
						ins.oid = pageId
						If height = 0 Then
							ins.key = getItem(b, maxItems - m)
							setnItems(pg, max - m + 1)
							setnItems(b, m)
						Else
							ins.key = getItem(b, m - 1)
							setnItems(pg, max - m)
							setnItems(b, m - 1)
						End If
						db.pool.unfix(b)
						Return OldBtreeResult.Overflow
					End If
				Finally
					If pg IsNot Nothing Then
						db.pool.unfix(pg)
					End If
				End Try
			End Function

			Friend Shared Function handlePageUnderflow(db As DatabaseImpl, pg As Page, r As Integer, height As Integer) As OldBtreeResult
				Dim nItems As Integer = getnItems(pg)
				Dim a As Page = db.putPage(getItem(pg, maxItems - r - 1))
				Dim an As Integer = getnItems(a)
				If r < nItems Then
					' exists greater page
					Dim b As Page = db.getPage(getItem(pg, maxItems - r - 2))
					Dim bn As Integer = getnItems(b)
					Debug.Assert(bn >= an)
					If height <> 1 Then
						memcpy(a, an, pg, r, 1)
						an += 1
						bn += 1
					End If
					If an + bn > max Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						db.pool.unfix(b)
						b = db.putPage(getItem(pg, maxItems - r - 2))
						memcpy(a, an, b, 0, i)
						memcpy(b, 0, b, i, bn - i)
						memcpy(a, maxItems - an - i, b, maxItems - i, i)
						memcpy(b, maxItems - bn + i, b, maxItems - bn, bn - i)
						If height <> 1 Then
							memcpy(pg, r, a, an + i - 1, 1)
						Else
							memcpy(pg, r, a, maxItems - an - i, 1)
						End If
						setnItems(b, getnItems(b) - i)
						setnItems(a, getnItems(a) + i)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return OldBtreeResult.Done
					Else
						' merge page b to a  
						memcpy(a, an, b, 0, bn)
						memcpy(a, maxItems - an - bn, b, maxItems - bn, bn)
						db.freePage(getItem(pg, maxItems - r - 2))
						memcpy(pg, maxItems - nItems, pg, maxItems - nItems - 1, nItems - r - 1)
						memcpy(pg, r, pg, r + 1, nItems - r - 1)
						setnItems(a, getnItems(a) + bn)
						setnItems(pg, nItems - 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return If(nItems < max \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
					End If
				Else
					' page b is before a
					Dim b As Page = db.getPage(getItem(pg, maxItems - r))
					Dim bn As Integer = getnItems(b)
					Debug.Assert(bn >= an)
					If height <> 1 Then
						an += 1
						bn += 1
					End If
					If an + bn > max Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						db.pool.unfix(b)
						b = db.putPage(getItem(pg, maxItems - r))
						memcpy(a, i, a, 0, an)
						memcpy(a, 0, b, bn - i, i)
						memcpy(a, maxItems - an - i, a, maxItems - an, an)
						memcpy(a, maxItems - i, b, maxItems - bn, i)
						If height <> 1 Then
							memcpy(a, i - 1, pg, r - 1, 1)
							memcpy(pg, r - 1, b, bn - i - 1, 1)
						Else
							memcpy(pg, r - 1, b, maxItems - bn + i, 1)
						End If
						setnItems(b, getnItems(b) - i)
						setnItems(a, getnItems(a) + i)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return OldBtreeResult.Done
					Else
						' merge page b to a
						memcpy(a, bn, a, 0, an)
						memcpy(a, 0, b, 0, bn)
						memcpy(a, maxItems - an - bn, a, maxItems - an, an)
						memcpy(a, maxItems - bn, b, maxItems - bn, bn)
						If height <> 1 Then
							memcpy(a, bn - 1, pg, r - 1, 1)
						End If
						db.freePage(getItem(pg, maxItems - r))
						setItem(pg, maxItems - r, getItem(pg, maxItems - r - 1))
						setnItems(a, getnItems(a) + bn)
						setnItems(pg, nItems - 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return If(nItems < max \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
					End If
				End If
			End Function

			Friend Shared Function remove(db As DatabaseImpl, pageId As Integer, oid As Integer, height As Integer) As OldBtreeResult
				Dim pg As Page = db.getPage(pageId)
				Try
					Dim i As Integer, n As Integer = getnItems(pg), l As Integer = 0, r As Integer = n
					If System.Threading.Interlocked.Decrement(height) = 0 Then
						While l < r
							i = (l + r) >> 1
							If oid > getItem(pg, maxItems - 1 - i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						If r < n AndAlso getItem(pg, maxItems - r - 1) = oid Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							memcpy(pg, r, pg, r + 1, n - r - 1)
							memcpy(pg, maxItems - n + 1, pg, maxItems - n, n - r - 1)
							setnItems(pg, System.Threading.Interlocked.Decrement(n))
							Return If(n < max \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
						End If
						Return OldBtreeResult.NotFound
					Else
						While l < r
							i = (l + r) >> 1
							If oid > getItem(pg, i) Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Dim result As OldBtreeResult = remove(db, getItem(pg, maxItems - r - 1), oid, height)
						If result = OldBtreeResult.Underflow Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							Return handlePageUnderflow(db, pg, r, height)
						End If
						Return result
					End If
				Finally
					If pg IsNot Nothing Then
						db.pool.unfix(pg)
					End If
				End Try
			End Function
		End Class
	End Class
End Namespace
#End If
