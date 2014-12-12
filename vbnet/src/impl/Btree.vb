Imports System.Collections
Imports System.Diagnostics
Imports Volante
Imports System.Collections.Generic
Imports ILink = ILink(Of IPersistent)
Namespace Volante.Impl

	Class Btree(Of K, V As {Class, IPersistent})
		Inherits PersistentCollection(Of V)
		Implements IIndex(Of K, V)
		Public ReadOnly Property KeyType() As Type
			Get
				Return GetType(K)
			End Get
		End Property
		Friend height As Integer
		Friend type As ClassDescriptor.FieldType
		Friend nElems As Integer
		Friend unique As Boolean
		Friend root As BtreePage

		<NonSerialized> _
		Friend updateCounter As Integer

		Friend Sub New()
		End Sub

		Public Overrides Sub OnLoad()
			If type <> ClassDescriptor.getTypeCode(GetType(K)) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE, GetType(K))
			End If
		End Sub

		Friend Class BtreeKey
			Friend key As Key
			Friend node As IPersistent
			Friend oldNode As IPersistent

			Friend Sub New(key As Key, node As IPersistent)
				Me.key = key
				Me.node = node
			End Sub
		End Class

		Friend MustInherit Class BtreePage
			Inherits Persistent
			Friend MustOverride ReadOnly Property Data() As Array
			Friend nItems As Integer
			Friend items As ILink

			Friend Const BTREE_PAGE_SIZE As Integer = Page.pageSize - ObjectHeader.Sizeof - 4 * 3

			Friend MustOverride Function getKeyValue(i As Integer) As Object
			Friend MustOverride Function getKey(i As Integer) As Key
			Friend MustOverride Function compare(key As Key, i As Integer) As Integer
			Friend MustOverride Sub insert(key As BtreeKey, i As Integer)
			Friend MustOverride Function clonePage() As BtreePage

			Friend Overridable Sub clearKeyValue(i As Integer)
			End Sub

			Friend Overridable Function find(firstKey As Key, lastKey As Key, height As Integer, result As ArrayList) As Boolean
				Dim l As Integer = 0, n As Integer = nItems, r As Integer = n
				height -= 1
				If firstKey IsNot Nothing Then
					While l < r
						Dim i As Integer = (l + r) >> 1
						If compare(firstKey, i) >= firstKey.inclusion Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(r = l)
				End If
				If lastKey IsNot Nothing Then
					If height = 0 Then
						While l < n
							If -compare(lastKey, l) >= lastKey.inclusion Then
								Return False
							End If
							result.Add(items(l))
							l += 1
						End While
						Return True
					Else
						Do
							If Not DirectCast(items(l), BtreePage).find(firstKey, lastKey, height, result) Then
								Return False
							End If

							If l = n Then
								Return True
							End If
						Loop While compare(lastKey, System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)) >= 0
						Return False
					End If
				End If
				If height = 0 Then
					While l < n
						result.Add(items(l))
						l += 1
					End While
				Else
					Do
						If Not DirectCast(items(l), BtreePage).find(firstKey, lastKey, height, result) Then
							Return False
						End If
					Loop While System.Threading.Interlocked.Increment(l) <= n
				End If
				Return True
			End Function

			Friend Shared Sub memcpyData(dst_pg As BtreePage, dst_idx As Integer, src_pg As BtreePage, src_idx As Integer, len As Integer)
				Array.Copy(src_pg.Data, src_idx, dst_pg.Data, dst_idx, len)
			End Sub

			Friend Shared Sub memcpyItems(dst_pg As BtreePage, dst_idx As Integer, src_pg As BtreePage, src_idx As Integer, len As Integer)
				Array.Copy(src_pg.items.ToRawArray(), src_idx, dst_pg.items.ToRawArray(), dst_idx, len)
			End Sub

			Friend Shared Sub memcpy(dst_pg As BtreePage, dst_idx As Integer, src_pg As BtreePage, src_idx As Integer, len As Integer)
				memcpyData(dst_pg, dst_idx, src_pg, src_idx, len)
				memcpyItems(dst_pg, dst_idx, src_pg, src_idx, len)
			End Sub

			Friend Overridable Sub memset(i As Integer, len As Integer)
				While System.Threading.Interlocked.Decrement(len) >= 0
					items(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1)) = Nothing
				End While
			End Sub

			Friend Overridable Function insert(ins As BtreeKey, height As Integer, unique As Boolean, overwrite As Boolean) As OperationResult
				Dim result As OperationResult
				Dim l As Integer = 0, n As Integer = nItems, r As Integer = n
				While l < r
					Dim i As Integer = (l + r) >> 1
					If compare(ins.key, i) > 0 Then
						l = i + 1
					Else
						r = i
					End If
				End While
				Debug.Assert(l = r)
				' insert before e[r] 

				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					result = DirectCast(items(r), BtreePage).insert(ins, height, unique, overwrite)
					Debug.Assert(result <> OperationResult.NotFound)
					If result <> OperationResult.Overflow Then
						Return result
					End If

					n += 1
				ElseIf r < n AndAlso compare(ins.key, r) = 0 Then
					If overwrite Then
						ins.oldNode = items(r)
						Modify()
						items(r) = ins.node
						Return OperationResult.Overwrite
					ElseIf unique Then
						ins.oldNode = items(r)
						Return OperationResult.Duplicate
					End If
				End If
				Dim max As Integer = items.Length
				Modify()
				If n < max Then
					memcpy(Me, r + 1, Me, r, n - r)
					insert(ins, r)
					nItems += 1
					Return OperationResult.Done
				Else
					' page is full then divide page 

					Dim b As BtreePage = clonePage()
					Debug.Assert(n = max)
					Dim m As Integer = max \ 2
					If r < m Then
						memcpy(b, 0, Me, 0, r)
						memcpy(b, r + 1, Me, r, m - r - 1)
						memcpy(Me, 0, Me, m - 1, max - m + 1)
						b.insert(ins, r)
					Else
						memcpy(b, 0, Me, 0, m)
						memcpy(Me, 0, Me, m, r - m)
						memcpy(Me, r - m + 1, Me, r, max - r)
						insert(ins, r - m)
					End If
					memset(max - m + 1, m - 1)
					ins.node = b
					ins.key = b.getKey(m - 1)
					If height = 0 Then
						nItems = max - m + 1
						b.nItems = m
					Else
						b.clearKeyValue(m - 1)
						nItems = max - m
						b.nItems = m - 1
					End If
					Return OperationResult.Overflow
				End If
			End Function

			Friend Overridable Function handlePageUnderflow(r As Integer, [rem] As BtreeKey, height As Integer) As OperationResult
				Dim a As BtreePage = DirectCast(items(r), BtreePage)
				a.Modify()
				Modify()
				Dim an As Integer = a.nItems
				If r < nItems Then
					' exists greater page
					Dim b As BtreePage = DirectCast(items(r + 1), BtreePage)
					Dim bn As Integer = b.nItems
					Debug.Assert(bn >= an)
					If height <> 1 Then
						memcpyData(a, an, Me, r, 1)
						an += 1
						bn += 1
					End If
					If an + bn > items.Length Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						b.Modify()
						memcpy(a, an, b, 0, i)
						memcpy(b, 0, b, i, bn - i)
						memcpyData(Me, r, a, an + i - 1, 1)
						If height <> 1 Then
							a.clearKeyValue(an + i - 1)
						End If

						b.memset(bn - i, i)
						b.nItems -= i
						a.nItems += i
						Return OperationResult.Done
					Else
						' merge page b to a  
						memcpy(a, an, b, 0, bn)
						b.Deallocate()
						memcpyData(Me, r, Me, r + 1, nItems - r - 1)
						memcpyItems(Me, r + 1, Me, r + 2, nItems - r - 1)
						items(nItems) = Nothing
						a.nItems += bn
						nItems -= 1
						Return If(nItems < (items.Size() >> 1), OperationResult.Underflow, OperationResult.Done)
					End If
				Else
					' page b is before a
					Dim b As BtreePage = DirectCast(items(r - 1), BtreePage)
					Dim bn As Integer = b.nItems
					Debug.Assert(bn >= an)
					If height <> 1 Then
						an += 1
						bn += 1
					End If
					If an + bn > items.Size() Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						b.Modify()
						memcpy(a, i, a, 0, an)
						memcpy(a, 0, b, bn - i, i)
						If height <> 1 Then
							memcpyData(a, i - 1, Me, r - 1, 1)
						End If

						memcpyData(Me, r - 1, b, bn - i - 1, 1)
						If height <> 1 Then
							b.clearKeyValue(bn - i - 1)
						End If

						b.memset(bn - i, i)
						b.nItems -= i
						a.nItems += i
						Return OperationResult.Done
					Else
						' merge page b to a
						memcpy(a, bn, a, 0, an)
						memcpy(a, 0, b, 0, bn)
						If height <> 1 Then
							memcpyData(a, bn - 1, Me, r - 1, 1)
						End If

						b.Deallocate()
						items(r - 1) = a
						items(nItems) = Nothing
						a.nItems += bn
						nItems -= 1
						Return If(nItems < (items.Size() >> 1), OperationResult.Underflow, OperationResult.Done)
					End If
				End If
			End Function

			Friend Overridable Function remove([rem] As BtreeKey, height As Integer) As OperationResult
				Dim i As Integer, n As Integer = nItems, l As Integer = 0, r As Integer = n

				While l < r
					i = (l + r) >> 1
					If compare([rem].key, i) > 0 Then
						l = i + 1
					Else
						r = i
					End If
				End While
				If System.Threading.Interlocked.Decrement(height) = 0 Then
					Dim node As IPersistent = [rem].node
					While r < n
						If compare([rem].key, r) = 0 Then
							If node Is Nothing OrElse items.ContainsElement(r, node) Then
								[rem].oldNode = items(r)
								Modify()
								memcpy(Me, r, Me, r + 1, n - r - 1)
								nItems = System.Threading.Interlocked.Decrement(n)
								memset(n, 1)
								Return If(n < (items.Size() >> 1), OperationResult.Underflow, OperationResult.Done)
							End If
						Else
							Exit While
						End If
						r += 1
					End While
					Return OperationResult.NotFound
				End If
				Do
					Select Case DirectCast(items(r), BtreePage).remove([rem], height)
						Case OperationResult.Underflow
							Return handlePageUnderflow(r, [rem], height)

						Case OperationResult.Done
							Return OperationResult.Done
					End Select
				Loop While System.Threading.Interlocked.Increment(r) <= n

				Return OperationResult.NotFound
			End Function

			Friend Overridable Sub purge(height As Integer)
				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					Dim n As Integer = nItems
					Do
						DirectCast(items(n), BtreePage).purge(height)
					Loop While System.Threading.Interlocked.Decrement(n) >= 0
				End If
				Deallocate()
			End Sub

			Friend Overridable Function traverseForward(height As Integer, result As IPersistent(), pos As Integer) As Integer
				Dim i As Integer, n As Integer = nItems
				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					For i = 0 To n
						pos = DirectCast(items(i), BtreePage).traverseForward(height, result, pos)
					Next
				Else
					For i = 0 To n - 1
						result(System.Math.Max(System.Threading.Interlocked.Increment(pos),pos - 1)) = items(i)
					Next
				End If
				Return pos
			End Function

			Friend Sub New(s As IDatabase, n As Integer)
				MyBase.New(s)
				items = s.CreateLink(Of IPersistent)(n)
				items.Length = n
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfByte
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property


			Protected m_data As Byte()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 1)

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfByte(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return CByte(key.ival) - m_data(i)
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CByte(key.key.ival)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Byte(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfSByte
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Private m_data As SByte()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 1)

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfSByte(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return CSByte(key.ival) - m_data(i)
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CSByte(key.key.ival)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New SByte(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfBoolean
			Inherits BtreePageOfByte
			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(data(i) <> 0)
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return data(i) <> 0
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfBoolean(Database)
			End Function

			Friend Sub New()
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s)
			End Sub
		End Class

		Private Class BtreePageOfShort
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As Short()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 2)


			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfShort(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return CShort(key.ival) - m_data(i)
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CShort(key.key.ival)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Short(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfUShort
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As UShort()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 2)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfUShort(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return CUShort(key.ival) - m_data(i)
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CUShort(key.key.ival)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New UShort(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfInt
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As Integer()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 4)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfInt(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				' Note: can't use key.ival - data[i] because
				' e.g. int.MaxVal - int.MinVal overflows
				If key.ival > m_data(i) Then
					Return 1
				End If
				If m_data(i) = key.ival Then
					Return 0
				End If
				Return -1
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = key.key.ival
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Integer(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfUInt
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As UInteger()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 4)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfUInt(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Dim uval As UInteger = CUInt(key.ival)
				If uval > m_data(i) Then
					Return 1
				End If
				If uval = m_data(i) Then
					Return 0
				End If
				Return -1
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CUInt(key.key.ival)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New UInteger(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfLong
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As Long()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 8)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfLong(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return If(key.lval < m_data(i), -1, If(key.lval = m_data(i), 0, 1))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = key.key.lval
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Long(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfULong
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As ULong()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 8)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfULong(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return If(CULng(key.lval) < m_data(i), -1, If(CULng(key.lval) = m_data(i), 0, 1))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CULng(key.key.lval)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New ULong(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfDate
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As ULong()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 8)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfDate(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Dim uval As ULong = CULng(key.lval)
				Return If(uval < m_data(i), -1, If(uval = m_data(i), 0, 1))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CULng(key.key.lval)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New ULong(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class


		Private Class BtreePageOfFloat
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property

			Friend m_data As Single()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 4)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfFloat(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return If(CSng(key.dval) < m_data(i), -1, If(CSng(key.dval) = m_data(i), 0, 1))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = CSng(key.key.dval)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Single(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfDouble
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property
			Friend m_data As Double()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 8)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfDouble(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return If(key.dval < m_data(i), -1, If(key.dval = m_data(i), 0, 1))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = key.key.dval
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Double(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfGuid
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property
			Friend m_data As Guid()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 16)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfGuid(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return key.guid.CompareTo(m_data(i))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = key.key.guid
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Guid(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfDecimal
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property
			Friend m_data As Decimal()

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 16)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfDecimal(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return key.dec.CompareTo(m_data(i))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = key.key.dec
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Decimal(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfObject
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data.ToRawArray()
				End Get
			End Property
			Friend m_data As ILink

			Const MAX_ITEMS As Integer = BTREE_PAGE_SIZE \ (4 + 4)

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data.GetRaw(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfObject(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return CInt(key.ival) - m_data(i).Oid
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = DirectCast(key.key.oval, IPersistent)
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = s.CreateLink(Of IPersistent)(MAX_ITEMS)
				m_data.Length = MAX_ITEMS
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfString
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return m_data
				End Get
			End Property
			Friend m_data As String()

			Friend Const MAX_ITEMS As Integer = 100

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(m_data(i))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return m_data(i)
			End Function

			Friend Overrides Sub clearKeyValue(i As Integer)
				m_data(i) = Nothing
			End Sub

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfString(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return DirectCast(key.oval, String).CompareTo(m_data(i))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				m_data(i) = DirectCast(key.key.oval, String)
			End Sub

			Friend Overrides Sub memset(i As Integer, len As Integer)
				While System.Threading.Interlocked.Decrement(len) >= 0
					items(i) = Nothing
					m_data(i) = Nothing
					i += 1
				End While
			End Sub

			Friend Overridable Function prefixSearch(prefix As String, height As Integer, result As ArrayList) As Boolean
				Dim l As Integer = 0, n As Integer = nItems, r As Integer = n
				height -= 1
				While l < r
					Dim i As Integer = (l + r) >> 1
					Dim s As String = m_data(i)
					' TODO: is s.StartsWith(prefix) needed at all?
					If Not s.StartsWith(prefix) AndAlso prefix.CompareTo(s) > 0 Then
						l = i + 1
					Else
						r = i
					End If
				End While
				Debug.Assert(r = l)
				If height = 0 Then
					While l < n
						If Not m_data(l).StartsWith(prefix) Then
							Return False
						End If

						result.Add(items(l))
						l += 1
					End While
				Else
					Do
						If Not DirectCast(items(l), BtreePageOfString).prefixSearch(prefix, height, result) Then
							Return False
						End If

						If l = n Then
							Return True
						End If
					Loop While m_data(System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)).StartsWith(prefix)
					Return False
				End If
				Return True
			End Function

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New String(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Private Class BtreePageOfRaw
			Inherits BtreePage
			Friend Overrides ReadOnly Property Data() As Array
				Get
					Return DirectCast(m_data, Array)
				End Get
			End Property
			Friend m_data As Object

			Friend Const MAX_ITEMS As Integer = 100

			Friend Overrides Function getKey(i As Integer) As Key
				Return New Key(DirectCast(DirectCast(m_data, Object())(i), IComparable))
			End Function

			Friend Overrides Function getKeyValue(i As Integer) As Object
				Return DirectCast(m_data, Object())(i)
			End Function

			Friend Overrides Sub clearKeyValue(i As Integer)
				DirectCast(m_data, Object())(i) = Nothing
			End Sub

			Friend Overrides Function clonePage() As BtreePage
				Return New BtreePageOfRaw(Database)
			End Function

			Friend Overrides Function compare(key As Key, i As Integer) As Integer
				Return DirectCast(key.oval, IComparable).CompareTo(DirectCast(m_data, Object())(i))
			End Function

			Friend Overrides Sub insert(key As BtreeKey, i As Integer)
				items(i) = key.node
				DirectCast(m_data, Object())(i) = key.key.oval
			End Sub

			Friend Sub New(s As IDatabase)
				MyBase.New(s, MAX_ITEMS)
				m_data = New Object(MAX_ITEMS - 1) {}
			End Sub

			Friend Sub New()
			End Sub
		End Class

		Friend Shared Function checkType(c As Type) As ClassDescriptor.FieldType
			Dim elemType As ClassDescriptor.FieldType = ClassDescriptor.getTypeCode(c)
			If CInt(elemType) > CInt(ClassDescriptor.FieldType.tpOid) AndAlso elemType <> ClassDescriptor.FieldType.tpDecimal AndAlso elemType <> ClassDescriptor.FieldType.tpRaw AndAlso elemType <> ClassDescriptor.FieldType.tpGuid Then
				Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE, c)
			End If
			Return elemType
		End Function

		Friend Sub New(indexType__1 As IndexType)
			type = checkType(GetType(K))
			Me.unique = (indexType__1 = IndexType.Unique)
		End Sub

		Friend Sub New(type As ClassDescriptor.FieldType, unique As Boolean)
			Me.type = type
			Me.unique = unique
		End Sub

		Friend Enum OperationResult
			Done
			Overflow
			Underflow
			NotFound
			Duplicate
			Overwrite
		End Enum

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return nElems
			End Get
		End Property

		Public Default Property Item(key As K) As V
			Get
				Return [Get](key)
			End Get
			Set
				[Set](key, value)
			End Set
		End Property

		Public Default ReadOnly Property Item(from As K, till As K) As V()
			Get
				Return [Get](from, till)
			End Get
		End Property

		Friend Overridable Function checkKey(key As Key) As Key
			If key Is Nothing Then
				Return Nothing
			End If

			If key.type <> type Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If

			If (type = ClassDescriptor.FieldType.tpObject OrElse type = ClassDescriptor.FieldType.tpOid) AndAlso key.ival = 0 AndAlso key.oval IsNot Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
			End If

			If TypeOf key.oval Is Char() Then
				key = New Key(New String(DirectCast(key.oval, Char())), key.inclusion <> 0)
			End If

			Return key
		End Function

		Public Overridable Function [Get](key As Key) As V
			key = checkKey(key)
			If root Is Nothing Then
				Return Nothing
			End If

			Dim list As New ArrayList()
			root.find(key, key, height, list)
			If list.Count > 1 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			ElseIf list.Count = 0 Then
				Return Nothing
			Else
				Return DirectCast(list(0), V)
			End If
		End Function

		Public Overridable Function [Get](key As K) As V
			Return [Get](KeyBuilder.getKeyFromObject(key))
		End Function

		Friend Shared emptySelection As V() = New V(-1) {}

		Public Overridable Function PrefixSearch(key As String) As V()
			If ClassDescriptor.FieldType.tpString <> type Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If

			If root Is Nothing Then
				Return emptySelection
			End If

			Dim list As New ArrayList()
			DirectCast(root, BtreePageOfString).prefixSearch(key, height, list)
			If list.Count <> 0 Then
				Return DirectCast(list.ToArray(GetType(V)), V())
			End If

			Return emptySelection
		End Function

		Public Overridable Function [Get](from As Key, till As Key) As V()
			If root Is Nothing Then
				Return emptySelection
			End If

			Dim list As New ArrayList()
			root.find(checkKey(from), checkKey(till), height, list)
			If list.Count <> 0 Then
				Return DirectCast(list.ToArray(GetType(V)), V())
			End If
			Return emptySelection
		End Function

		Public Overridable Function [Get](from As K, till As K) As V()
			Return [Get](KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till))
		End Function

		Public Overridable Function Put(key As Key, obj As V) As Boolean
			Return insert(key, obj, False) Is Nothing
		End Function

		Public Overridable Function Put(key As K, obj As V) As Boolean
			Return Put(KeyBuilder.getKeyFromObject(key), obj)
		End Function

		Public Overridable Function [Set](key As Key, obj As V) As V
			Return DirectCast(insert(key, obj, True), V)
		End Function

		Public Overridable Function [Set](key As K, obj As V) As V
			Return [Set](KeyBuilder.getKeyFromObject(key), obj)
		End Function

		Friend Sub allocateRootPage(ins As BtreeKey)
			Dim s As IDatabase = Database
			Dim newRoot As BtreePage = Nothing
			Select Case type
				Case ClassDescriptor.FieldType.tpByte
					newRoot = New BtreePageOfByte(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpSByte
					newRoot = New BtreePageOfSByte(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpShort
					newRoot = New BtreePageOfShort(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpUShort
					newRoot = New BtreePageOfUShort(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpBoolean
					newRoot = New BtreePageOfBoolean(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpOid
					newRoot = New BtreePageOfInt(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpUInt
					newRoot = New BtreePageOfUInt(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpLong
					newRoot = New BtreePageOfLong(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpULong
					newRoot = New BtreePageOfULong(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpDate
					newRoot = New BtreePageOfDate(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpFloat
					newRoot = New BtreePageOfFloat(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpDouble
					newRoot = New BtreePageOfDouble(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpObject
					newRoot = New BtreePageOfObject(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpString
					newRoot = New BtreePageOfString(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpRaw
					newRoot = New BtreePageOfRaw(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpDecimal
					newRoot = New BtreePageOfDecimal(s)
					Exit Select

				Case ClassDescriptor.FieldType.tpGuid
					newRoot = New BtreePageOfGuid(s)
					Exit Select
				Case Else

					Debug.Assert(False, "Invalid type")
					Exit Select
			End Select
			newRoot.insert(ins, 0)
			newRoot.items(1) = root
			newRoot.nItems = 1
			root = newRoot
		End Sub

		Friend Function insert(key As Key, obj As IPersistent, overwrite As Boolean) As IPersistent
			Dim ins As New BtreeKey(checkKey(key), obj)
			If root Is Nothing Then
				allocateRootPage(ins)
				height = 1
			Else
				Dim result As OperationResult = root.insert(ins, height, unique, overwrite)
				If result = OperationResult.Overflow Then
					allocateRootPage(ins)
					height += 1
				ElseIf result = OperationResult.Duplicate OrElse result = OperationResult.Overwrite Then
					Return ins.oldNode
				End If
			End If
			updateCounter += 1
			nElems += 1
			Modify()
			Return Nothing
		End Function

		Public Overridable Sub Remove(key As Key, obj As V)
			Remove(New BtreeKey(checkKey(key), obj))
		End Sub

		Public Overridable Sub Remove(key As K, obj As V)
			Remove(New BtreeKey(KeyBuilder.getKeyFromObject(key), obj))
		End Sub

		Friend Overridable Sub Remove([rem] As BtreeKey)
			If root Is Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If

			Dim result As OperationResult = root.remove([rem], height)
			If result = OperationResult.NotFound Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If

			nElems -= 1
			If result = OperationResult.Underflow Then
				If root.nItems = 0 Then
					Dim newRoot As BtreePage = Nothing
					If height <> 1 Then
						newRoot = DirectCast(root.items(0), BtreePage)
					End If

					root.Deallocate()
					root = newRoot
					height -= 1
				End If
			End If
			updateCounter += 1
			Modify()
		End Sub

		Public Overridable Function Remove(key As Key) As V
			If Not unique Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			End If

			Dim rk As New BtreeKey(checkKey(key), Nothing)
			Remove(rk)
			Return DirectCast(rk.oldNode, V)
		End Function

		Public Overridable Function RemoveKey(key As K) As V
			Return Remove(KeyBuilder.getKeyFromObject(key))
		End Function

		Public Overridable Function GetPrefix(prefix As String) As V()
			Return [Get](New Key(prefix, True), New Key(prefix & [Char].MaxValue, False))
		End Function

		Public Overrides Sub Clear()
			If root Is Nothing Then
				Return
			End If

			root.purge(height)
			root = Nothing
			nElems = 0
			height = 0
			updateCounter += 1
			Modify()
		End Sub

		Public Overridable Function ToArray() As V()
			Dim arr As V() = New V(nElems - 1) {}
			If root IsNot Nothing Then
				root.traverseForward(height, arr, 0)
			End If

			Return DirectCast(arr, V())
		End Function

		Public Overrides Sub Deallocate()
			If root IsNot Nothing Then
				root.purge(height)
			End If

			MyBase.Deallocate()
		End Sub

		Private Class BtreeEnumerator
			Implements IEnumerator(Of V)
			Friend Sub New(tree As Btree(Of K, V))
				Me.tree = tree
				Reset()
			End Sub

			Public Sub Reset() Implements IEnumerator.Reset
				Dim page As BtreePage = tree.root
				Dim h As Integer = tree.height
				counter = tree.updateCounter
				pageStack = New BtreePage(h - 1) {}
				posStack = New Integer(h - 1) {}
				sp = 0
				If h = 0 Then
					Return
				End If

				Debug.Assert(h > 0)

				While System.Threading.Interlocked.Decrement(h) > 0
					posStack(sp) = 0
					pageStack(sp) = page
					page = DirectCast(page.items(0), BtreePage)
					sp += 1
				End While
				posStack(sp) = 0
				pageStack(sp) = page
				[end] = page.nItems
				sp += 1
			End Sub

			Protected Overridable Sub getCurrent(pg As BtreePage, pos As Integer)
				curr = pg.items(pos)
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If counter <> tree.updateCounter Then
					Throw New InvalidOperationException("B-Tree was modified")
				End If

				If sp > 0 AndAlso posStack(sp - 1) < [end] Then
					Dim pos As Integer = posStack(sp - 1)
					Dim pg As BtreePage = pageStack(sp - 1)
					getCurrent(pg, pos)
					hasCurrent = True
					If System.Threading.Interlocked.Increment(pos) = [end] Then
						While System.Threading.Interlocked.Decrement(sp) <> 0
							pos = posStack(sp - 1)
							pg = pageStack(sp - 1)
							If System.Threading.Interlocked.Increment(pos) <= pg.nItems Then
								posStack(sp - 1) = pos
								Do
									pg = DirectCast(pg.items(pos), BtreePage)
									[end] = pg.nItems
									pageStack(sp) = pg
									posStack(sp) = InlineAssignHelper(pos, 0)
								Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
								Exit While
							End If
						End While
					Else
						posStack(sp - 1) = pos
					End If
					Return True
				End If
				hasCurrent = False
				Return False
			End Function

			Public Overridable ReadOnly Property Current() As V Implements IEnumerator(Of V).Current
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return DirectCast(curr, V)
				End Get
			End Property
			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property
			Protected pageStack As BtreePage()
			Protected posStack As Integer()
			Protected sp As Integer
			Protected [end] As Integer
			Protected counter As Integer
			Protected curr As IPersistent
			Protected hasCurrent As Boolean
			Protected tree As Btree(Of K, V)
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Private Class BtreeDictionaryEnumerator
			Inherits BtreeEnumerator
			Implements IDictionaryEnumerator
			Friend Sub New(tree As Btree(Of K, V))
				MyBase.New(tree)
			End Sub

			Protected Overrides Sub getCurrent(pg As BtreePage, pos As Integer)
				MyBase.getCurrent(pg, pos)
				m_key = pg.getKeyValue(pos)
			End Sub

			Public Overridable Shadows ReadOnly Property Current() As Object Implements IEnumerator.Current
				Get
					Return Entry
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return New DictionaryEntry(m_key, curr)
				End Get
			End Property

			Public ReadOnly Property Key() As Object Implements IDictionaryEnumerator.Key
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return m_key
				End Get
			End Property

			Public ReadOnly Property Value() As Object Implements IDictionaryEnumerator.Value
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return curr
				End Get
			End Property

			Protected m_key As Object
		End Class

		Public Overrides Function GetEnumerator() As IEnumerator(Of V)
			Return New BtreeEnumerator(Me)
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return DirectCast(New BtreeEnumerator(Me), IEnumerator)
		End Function

		Public Function GetDictionaryEnumerator() As IDictionaryEnumerator
			Return New BtreeDictionaryEnumerator(Me)
		End Function

		Private Class BtreeSelectionIterator
			Implements IEnumerator(Of V)
			Implements IEnumerable(Of V)
			Friend Sub New(tree As Btree(Of K, V), from As Key, till As Key, order As IterationOrder)
				Me.from = from
				Me.till = till
				Me.order = order
				Me.tree = tree
				Reset()
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of V) Implements IEnumerable(Of V).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return Me
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				Dim i As Integer, l As Integer, r As Integer

				sp = 0
				counter = tree.updateCounter
				If tree.height = 0 Then
					Return
				End If

				Dim page As BtreePage = tree.root
				Dim h As Integer = tree.height

				pageStack = New BtreePage(h - 1) {}
				posStack = New Integer(h - 1) {}

				If order = IterationOrder.AscentOrder Then
					If from Is Nothing Then
						While System.Threading.Interlocked.Decrement(h) > 0
							posStack(sp) = 0
							pageStack(sp) = page
							page = DirectCast(page.items(0), BtreePage)
							sp += 1
						End While
						posStack(sp) = 0
						pageStack(sp) = page
						[end] = page.nItems
						sp += 1
					Else
						While System.Threading.Interlocked.Decrement(h) > 0
							pageStack(sp) = page
							l = 0
							r = page.nItems
							While l < r
								i = (l + r) >> 1
								If page.compare(from, i) >= from.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							posStack(sp) = r
							page = DirectCast(page.items(r), BtreePage)
							sp += 1
						End While
						pageStack(sp) = page
						l = 0
						r = InlineAssignHelper([end], page.nItems)
						While l < r
							i = (l + r) >> 1
							If page.compare(from, i) >= from.inclusion Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(r = l)
						If r = [end] Then
							sp += 1
							gotoNextItem(page, r - 1)
						Else
							posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r
						End If
					End If
					If sp <> 0 AndAlso till IsNot Nothing Then
						page = pageStack(sp - 1)
						If -page.compare(till, posStack(sp - 1)) >= till.inclusion Then
							sp = 0
						End If
					End If
				Else
					' descent order
					If till Is Nothing Then
						While System.Threading.Interlocked.Decrement(h) > 0
							pageStack(sp) = page
							posStack(sp) = page.nItems
							page = DirectCast(page.items(page.nItems), BtreePage)
							sp += 1
						End While
						pageStack(sp) = page
						posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = page.nItems - 1
					Else
						While System.Threading.Interlocked.Decrement(h) > 0
							pageStack(sp) = page
							l = 0
							r = page.nItems
							While l < r
								i = (l + r) >> 1
								If page.compare(till, i) >= 1 - till.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							posStack(sp) = r
							page = DirectCast(page.items(r), BtreePage)
							sp += 1
						End While
						pageStack(sp) = page
						l = 0
						r = page.nItems
						While l < r
							i = (l + r) >> 1
							If page.compare(till, i) >= 1 - till.inclusion Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(r = l)
						If r = 0 Then
							sp += 1
							gotoNextItem(page, r)
						Else
							posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r - 1
						End If
					End If
					If sp <> 0 AndAlso from IsNot Nothing Then
						page = pageStack(sp - 1)
						If page.compare(from, posStack(sp - 1)) >= from.inclusion Then
							sp = 0
						End If
					End If
				End If
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If counter <> tree.updateCounter Then
					Throw New InvalidOperationException("B-Tree was modified")
				End If

				If 0 = sp Then
					hasCurrent = False
					Return False
				End If

				Dim pos As Integer = posStack(sp - 1)
				Dim pg As BtreePage = pageStack(sp - 1)
				hasCurrent = True
				getCurrent(pg, pos)
				gotoNextItem(pg, pos)
				Return True
			End Function

			Protected Overridable Sub getCurrent(pg As BtreePage, pos As Integer)
				curr = pg.items(pos)
			End Sub

			Public Overridable ReadOnly Property Current() As V Implements IEnumerator(Of V).Current
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return DirectCast(curr, V)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Protected Friend Sub gotoNextItem(pg As BtreePage, pos As Integer)
				If order = IterationOrder.AscentOrder Then
					If System.Threading.Interlocked.Increment(pos) = [end] Then
						While System.Threading.Interlocked.Decrement(sp) <> 0
							pos = posStack(sp - 1)
							pg = pageStack(sp - 1)
							If System.Threading.Interlocked.Increment(pos) <= pg.nItems Then
								posStack(sp - 1) = pos
								Do
									pg = DirectCast(pg.items(pos), BtreePage)
									[end] = pg.nItems
									pageStack(sp) = pg
									posStack(sp) = InlineAssignHelper(pos, 0)
								Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
								Exit While
							End If
						End While
					Else
						posStack(sp - 1) = pos
					End If
					If sp <> 0 AndAlso till IsNot Nothing AndAlso -pg.compare(till, pos) >= till.inclusion Then
						sp = 0
					End If
				Else
					' descent order
					If System.Threading.Interlocked.Decrement(pos) < 0 Then
						While System.Threading.Interlocked.Decrement(sp) <> 0
							pos = posStack(sp - 1)
							pg = pageStack(sp - 1)
							If System.Threading.Interlocked.Decrement(pos) >= 0 Then
								posStack(sp - 1) = pos
								Do
									pg = DirectCast(pg.items(pos), BtreePage)
									pageStack(sp) = pg
									posStack(sp) = InlineAssignHelper(pos, pg.nItems)
								Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
								posStack(sp - 1) = System.Threading.Interlocked.Decrement(pos)
								Exit While
							End If
						End While
					Else
						posStack(sp - 1) = pos
					End If
					If sp <> 0 AndAlso from IsNot Nothing AndAlso pg.compare(from, pos) >= from.inclusion Then
						sp = 0
					End If
				End If
			End Sub

			Protected pageStack As BtreePage()
			Protected posStack As Integer()
			Protected sp As Integer
			Protected [end] As Integer
			Protected from As Key
			Protected till As Key
			Protected order As IterationOrder
			Protected counter As Integer
			Protected hasCurrent As Boolean
			Protected curr As IPersistent
			Protected tree As Btree(Of K, V)
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Private Class BtreeDictionarySelectionIterator
			Inherits BtreeSelectionIterator
			Implements IDictionaryEnumerator
			Friend Sub New(tree As Btree(Of K, V), from As Key, till As Key, order As IterationOrder)
				MyBase.New(tree, from, till, order)
			End Sub

			Protected Overrides Sub getCurrent(pg As BtreePage, pos As Integer)
				MyBase.getCurrent(pg, pos)
				m_key = pg.getKeyValue(pos)
			End Sub

			Public Overridable Shadows ReadOnly Property Current() As Object Implements IEnumerator.Current
				Get
					Return Entry
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return New DictionaryEntry(m_key, curr)
				End Get
			End Property

			Public ReadOnly Property Key() As Object Implements IDictionaryEnumerator.Key
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return m_key
				End Get
			End Property

			Public ReadOnly Property Value() As Object Implements IDictionaryEnumerator.Value
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return curr
				End Get
			End Property

			Protected m_key As Object
		End Class

		Public Function GetEnumerator(from As Key, till As Key, order As IterationOrder) As IEnumerator(Of V)
			Return Range(from, till, order).GetEnumerator()
		End Function

		Public Function GetEnumerator(from As K, till As K, order As IterationOrder) As IEnumerator(Of V)
			Return Range(from, till, order).GetEnumerator()
		End Function

		Public Function GetEnumerator(from As Key, till As Key) As IEnumerator(Of V)
			Return Range(from, till).GetEnumerator()
		End Function

		Public Function GetEnumerator(from As K, till As K) As IEnumerator(Of V)
			Return Range(from, till).GetEnumerator()
		End Function

		Public Function GetEnumerator(prefix As String) As IEnumerator(Of V)
			Return StartsWith(prefix).GetEnumerator()
		End Function

		Public Overridable Function Range(from As Key, till As Key, order As IterationOrder) As IEnumerable(Of V)
			Return New BtreeSelectionIterator(Me, checkKey(from), checkKey(till), order)
		End Function

		Public Overridable Function Range(from As Key, till As Key) As IEnumerable(Of V)
			Return Range(from, till, IterationOrder.AscentOrder)
		End Function

		Public Function Range(from As K, till As K, order As IterationOrder) As IEnumerable(Of V)
			Return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), order)
		End Function

		Public Function Range(from As K, till As K) As IEnumerable(Of V)
			Return Range(KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till), IterationOrder.AscentOrder)
		End Function

		Public Function Reverse() As IEnumerable(Of V)
			Return New BtreeSelectionIterator(Me, Nothing, Nothing, IterationOrder.DescentOrder)
		End Function

		Public Function StartsWith(prefix As String) As IEnumerable(Of V)
			Return Range(New Key(prefix), New Key(prefix & [Char].MaxValue, False), IterationOrder.AscentOrder)
		End Function

		Public Overridable Function GetDictionaryEnumerator(from As Key, till As Key, order As IterationOrder) As IDictionaryEnumerator
			Return New BtreeDictionarySelectionIterator(Me, checkKey(from), checkKey(till), order)
		End Function
	End Class
End Namespace
