#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Enum OldBtreeResult
		Done
		Overflow
		Underflow
		NotFound
		Duplicate
		Overwrite
	End Enum

	Interface OldBtree
		Inherits IPersistent
		Function markTree() As Integer
		#If WITH_XML Then
		Sub export(exporter As XmlExporter)
		#End If
		Function insert(key As Key, obj As IPersistent, overwrite As Boolean) As Integer
		ReadOnly Property FieldType() As ClassDescriptor.FieldType
		ReadOnly Property FieldTypes() As ClassDescriptor.FieldType()
		ReadOnly Property IsUnique() As Boolean
		Function compareByteArrays(key As Key, pg As Page, i As Integer) As Integer
		ReadOnly Property HeaderSize() As Integer
		Sub init(cls As Type, type As ClassDescriptor.FieldType, fieldNames As String(), unique As Boolean, autoincCount As Long)
	End Interface

	Class OldBtree(Of K, V As {Class, IPersistent})
		Inherits PersistentCollection(Of V)
		Implements IIndex(Of K, V)
		Inherits OldBtree
		Friend root As Integer
		Friend height As Integer
		Friend type As ClassDescriptor.FieldType
		Friend nElems As Integer
		Friend unique As Boolean
		<NonSerialized> _
		Friend updateCounter As Integer

		Friend Shared Sizeof As Integer = ObjectHeader.Sizeof + 4 * 4 + 1

		Friend Sub New()
		End Sub

		Friend Sub New(obj As Byte(), offs As Integer)
			root = Bytes.unpack4(obj, offs)
			offs += 4
			height = Bytes.unpack4(obj, offs)
			offs += 4
			type = DirectCast(Bytes.unpack4(obj, offs), ClassDescriptor.FieldType)
			offs += 4
			nElems = Bytes.unpack4(obj, offs)
			offs += 4
			unique = obj(offs) <> 0
		End Sub

		Friend Sub New(indexType__1 As IndexType)
			type = checkType(GetType(K))
			Me.unique = (indexType__1 = IndexType.Unique)
		End Sub

		Public Overrides Sub OnLoad()
			If type <> ClassDescriptor.getTypeCode(GetType(K)) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE, GetType(K))
			End If
		End Sub

		Friend Sub New(type As ClassDescriptor.FieldType, unique As Boolean)
			Me.type = type
			Me.unique = unique
		End Sub

		Public Overridable Sub init(cls As Type, type As ClassDescriptor.FieldType, fieldNames As String(), unique As Boolean, autoincCount As Long)
			Me.type = type
			Me.unique = unique
		End Sub

		Protected Shared Function checkType(c As Type) As ClassDescriptor.FieldType
			Dim elemType As ClassDescriptor.FieldType = ClassDescriptor.getTypeCode(c)
			If CInt(elemType) > CInt(ClassDescriptor.FieldType.tpOid) AndAlso elemType <> ClassDescriptor.FieldType.tpArrayOfByte AndAlso elemType <> ClassDescriptor.FieldType.tpDecimal AndAlso elemType <> ClassDescriptor.FieldType.tpGuid Then
				Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE, c)
			End If
			Return elemType
		End Function

		Public Overridable Function compareByteArrays(key As Byte(), item As Byte(), offs As Integer, length As Integer) As Integer
			Dim n As Integer = If(key.Length >= length, length, key.Length)
			For i As Integer = 0 To n - 1
				Dim diff As Integer = key(i) - item(i + offs)
				If diff <> 0 Then
					Return diff
				End If
			Next
			Return key.Length - length
		End Function

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return nElems
			End Get
		End Property

		Public ReadOnly Property IsUnique() As Boolean Implements OldBtree.IsUnique
			Get
				Return unique
			End Get
		End Property

		Public ReadOnly Property HeaderSize() As Integer Implements OldBtree.HeaderSize
			Get
				Return Sizeof
			End Get
		End Property

		Public ReadOnly Property FieldType() As ClassDescriptor.FieldType Implements OldBtree.FieldType
			Get
				Return type
			End Get
		End Property

		Public Overridable ReadOnly Property FieldTypes() As ClassDescriptor.FieldType() Implements OldBtree.FieldTypes
			Get
				Return New ClassDescriptor.FieldType() {type}
			End Get
		End Property

		Public ReadOnly Property KeyType() As Type
			Get
				Return GetType(K)
			End Get
		End Property

		Public Default ReadOnly Property Item(from As K, till As K) As V()
			Get
				Return [Get](from, till)
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

		Protected Function checkKey(key As Key) As Key
			If key Is Nothing Then
				Return Nothing
			End If

			If key.type <> type Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If

			If (type = ClassDescriptor.FieldType.tpObject OrElse type = ClassDescriptor.FieldType.tpOid) AndAlso key.ival = 0 AndAlso key.oval IsNot Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INVALID_OID)
			End If
			If type = ClassDescriptor.FieldType.tpString AndAlso TypeOf key.oval Is String Then
				key = New Key(DirectCast(key.oval, String).ToCharArray(), key.inclusion <> 0)
			End If

			Return key
		End Function

		Public Overridable Function [Get](key As Key) As V
			key = checkKey(key)
			If 0 = root Then
				Return Nothing
			End If

			Dim list As New ArrayList()
			OldBtreePage.find(DirectCast(Database, DatabaseImpl), root, key, key, Me, height, _
				list)
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

		Public Overridable Function [Get](from As Key, till As Key) As V()
			If 0 = root Then
				Return emptySelection
			End If

			Dim list As New ArrayList()
			OldBtreePage.find(DirectCast(Database, DatabaseImpl), root, checkKey(from), checkKey(till), Me, height, _
				list)
			If 0 = list.Count Then
				Return emptySelection
			End If

			Return DirectCast(list.ToArray(GetType(V)), V())
		End Function

		Public Function PrefixSearch(key As String) As V()
			If ClassDescriptor.FieldType.tpString <> type Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If
			If 0 = root Then
				Return emptySelection
			End If

			Dim list As New ArrayList()
			OldBtreePage.prefixSearch(DirectCast(Database, DatabaseImpl), root, key, height, list)
			If list.Count <> 0 Then
				Return DirectCast(list.ToArray(GetType(V)), V())
			End If
			Return emptySelection
		End Function

		Public Overridable Function [Get](from As K, till As K) As V()
			Return [Get](KeyBuilder.getKeyFromObject(from), KeyBuilder.getKeyFromObject(till))
		End Function

		Public Overridable Function GetPrefix(prefix As String) As V()
			Return [Get](New Key(prefix.ToCharArray()), New Key((prefix & [Char].MaxValue).ToCharArray(), False))
		End Function

		Public Overridable Function Put(key As Key, obj As V) As Boolean
			Return insert(key, obj, False) >= 0
		End Function

		Public Overridable Function Put(key As K, obj As V) As Boolean
			Return Put(KeyBuilder.getKeyFromObject(key), obj)
		End Function

		Public Overridable Function [Set](key As Key, obj As V) As V
			Dim oid As Integer = insert(key, obj, True)
			Return If((oid <> 0), DirectCast(DirectCast(Database, DatabaseImpl).lookupObject(oid, Nothing), V), Nothing)
		End Function

		Public Overridable Function [Set](key As K, obj As V) As V
			Return [Set](KeyBuilder.getKeyFromObject(key), obj)
		End Function

		Public Function insert(key As Key, obj As IPersistent, overwrite As Boolean) As Integer
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			If db Is Nothing Then
				Throw New DatabaseException(Volante.DatabaseException.ErrorCode.DELETED_OBJECT)
			End If

			If Not obj.IsPersistent() Then
				db.MakePersistent(obj)
			End If

			Dim ins As New OldBtreeKey(checkKey(key), obj.Oid)
			If root = 0 Then
				root = OldBtreePage.allocate(db, 0, type, ins)
				height = 1
			Else
				Dim result As OldBtreeResult = OldBtreePage.insert(db, root, Me, ins, height, unique, _
					overwrite)
				If result = OldBtreeResult.Overflow Then
					root = OldBtreePage.allocate(db, root, type, ins)
					height += 1
				ElseIf result = OldBtreeResult.Duplicate Then
					Return -1
				ElseIf result = OldBtreeResult.Overwrite Then
					Return ins.oldOid
				End If
			End If
			nElems += 1
			updateCounter += 1
			Modify()
			Return 0
		End Function

		Public Overridable Sub Remove(key As Key, obj As V)
			remove(New OldBtreeKey(checkKey(key), obj.Oid))
		End Sub

		Public Overridable Sub Remove(key As K, obj As V)
			remove(New OldBtreeKey(KeyBuilder.getKeyFromObject(key), obj.Oid))
		End Sub

		Friend Overridable Sub remove([rem] As OldBtreeKey)
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			If db Is Nothing Then
				Throw New DatabaseException(Volante.DatabaseException.ErrorCode.DELETED_OBJECT)
			End If

			If root = 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If

			Dim result As OldBtreeResult = OldBtreePage.remove(db, root, Me, [rem], height)
			If result = OldBtreeResult.NotFound Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If

			nElems -= 1
			If result = OldBtreeResult.Underflow Then
				Dim pg As Page = db.getPage(root)
				If OldBtreePage.getnItems(pg) = 0 Then
					Dim newRoot As Integer = 0
					If height <> 1 Then
						newRoot = If((type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte), OldBtreePage.getKeyStrOid(pg, 0), OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1))
					End If
					db.freePage(root)
					root = newRoot
					height -= 1
				End If
				db.pool.unfix(pg)
			ElseIf result = OldBtreeResult.Overflow Then
				root = OldBtreePage.allocate(db, root, type, [rem])
				height += 1
			End If
			updateCounter += 1
			Modify()
		End Sub

		Public Overridable Function Remove(key As Key) As V
			If Not unique Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE)
			End If

			Dim rk As New OldBtreeKey(checkKey(key), 0)
			Dim db As DatabaseImpl = DirectCast(Database, DatabaseImpl)
			remove(rk)
			Return DirectCast(db.lookupObject(rk.oldOid, Nothing), V)
		End Function

		Public Overridable Function RemoveKey(key As K) As V
			Return Remove(KeyBuilder.getKeyFromObject(key))
		End Function

		Public Overrides Sub Clear()
			If 0 = root Then
				Return
			End If
			OldBtreePage.purge(DirectCast(Database, DatabaseImpl), root, type, height)
			root = 0
			nElems = 0
			height = 0
			updateCounter += 1
			Modify()
		End Sub

		Public Overridable Function ToArray() As V()
			Dim arr As V() = New V(nElems - 1) {}
			If root <> 0 Then
				OldBtreePage.traverseForward(DirectCast(Database, DatabaseImpl), root, type, height, arr, 0)
			End If
			Return arr
		End Function

		Public Overrides Sub Deallocate()
			If root <> 0 Then
				OldBtreePage.purge(DirectCast(Database, DatabaseImpl), root, type, height)
			End If
			MyBase.Deallocate()
		End Sub

		#If WITH_XML Then
		Public Sub export(exporter As XmlExporter)
			If root <> 0 Then
				OldBtreePage.exportPage(DirectCast(Database, DatabaseImpl), exporter, root, type, height)
			End If
		End Sub
		#End If

		Public Function markTree() As Integer Implements OldBtree.markTree
			Return If((root <> 0), OldBtreePage.markPage(DirectCast(Database, DatabaseImpl), root, type, height), 0)
		End Function

		Protected Overridable Function unpackEnum(val As Integer) As Object
			' Base B-Tree class has no information about particular enum type
			' so it is not able to correctly unpack enum key
			Return val
		End Function

		Friend Function unpackKey(db As DatabaseImpl, pg As Page, pos As Integer) As Object
			Dim offs As Integer = OldBtreePage.firstKeyOffs + pos * ClassDescriptor.Sizeof(CInt(type))
			Dim data As Byte() = pg.data

			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					Return data(offs) <> 0

				Case ClassDescriptor.FieldType.tpSByte
					Return CSByte(data(offs))

				Case ClassDescriptor.FieldType.tpByte
					Return data(offs)

				Case ClassDescriptor.FieldType.tpShort
					Return Bytes.unpack2(data, offs)

				Case ClassDescriptor.FieldType.tpUShort
					Return CUShort(Bytes.unpack2(data, offs))

				Case ClassDescriptor.FieldType.tpChar
					Return CChar(Bytes.unpack2(data, offs))

				Case ClassDescriptor.FieldType.tpInt
					Return Bytes.unpack4(data, offs)

				Case ClassDescriptor.FieldType.tpEnum
					Return unpackEnum(Bytes.unpack4(data, offs))

				Case ClassDescriptor.FieldType.tpUInt
					Return CUInt(Bytes.unpack4(data, offs))

				Case ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpObject
					Return db.lookupObject(Bytes.unpack4(data, offs), Nothing)

				Case ClassDescriptor.FieldType.tpLong
					Return Bytes.unpack8(data, offs)

				Case ClassDescriptor.FieldType.tpDate
					Return New DateTime(Bytes.unpack8(data, offs))

				Case ClassDescriptor.FieldType.tpULong
					Return CULng(Bytes.unpack8(data, offs))

				Case ClassDescriptor.FieldType.tpFloat
					Return Bytes.unpackF4(data, offs)

				Case ClassDescriptor.FieldType.tpDouble
					Return Bytes.unpackF8(data, offs)

				Case ClassDescriptor.FieldType.tpGuid
					Return Bytes.unpackGuid(data, offs)

				Case ClassDescriptor.FieldType.tpDecimal
					Return Bytes.unpackDecimal(data, offs)

				Case ClassDescriptor.FieldType.tpString
					If True Then
						Dim len As Integer = OldBtreePage.getKeyStrSize(pg, pos)
						offs = OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, pos)
						Dim sval As Char() = New Char(len - 1) {}
						For j As Integer = 0 To len - 1
							sval(j) = CChar(Bytes.unpack2(pg.data, offs))
							offs += 2
						Next
						Return New [String](sval)
					End If
				Case ClassDescriptor.FieldType.tpArrayOfByte
					If True Then
						Return unpackByteArrayKey(pg, pos)
					End If
				Case Else
					Debug.Assert(False, "Invalid type")
					Return Nothing
			End Select
		End Function

		Protected Overridable Function unpackByteArrayKey(pg As Page, pos As Integer) As Object
			Dim len As Integer = OldBtreePage.getKeyStrSize(pg, pos)
			Dim offs As Integer = OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, pos)
			Dim val As Byte() = New Byte(len - 1) {}
			Array.Copy(pg.data, offs, val, 0, len)
			Return val
		End Function

		Private Class BtreeEnumerator
			Implements IEnumerator(Of V)
			Friend Sub New(tree As OldBtree(Of K, V))
				Me.tree = tree
				Reset()
			End Sub

			Protected Overridable Function getReference(pg As Page, pos As Integer) As Integer
				Return OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - pos)
			End Function

			Protected Overridable Sub getCurrent(pg As Page, pos As Integer)
				oid = getReference(pg, pos)
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If updateCounter <> tree.updateCounter Then
					Throw New InvalidOperationException("B-Tree was modified")
				End If

				If sp > 0 AndAlso posStack(sp - 1) < [end] Then
					Dim pos As Integer = posStack(sp - 1)
					Dim pg As Page = db.getPage(pageStack(sp - 1))
					getCurrent(pg, pos)
					hasCurrent = True
					If System.Threading.Interlocked.Increment(pos) = [end] Then
						While System.Threading.Interlocked.Decrement(sp) <> 0
							db.pool.unfix(pg)
							pos = posStack(sp - 1)
							pg = db.getPage(pageStack(sp - 1))
							If System.Threading.Interlocked.Increment(pos) <= OldBtreePage.getnItems(pg) Then
								posStack(sp - 1) = pos
								Do
									Dim pageId As Integer = getReference(pg, pos)
									db.pool.unfix(pg)
									pg = db.getPage(pageId)
									[end] = OldBtreePage.getnItems(pg)
									pageStack(sp) = pageId
									posStack(sp) = InlineAssignHelper(pos, 0)
								Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
								Exit While
							End If
						End While
					Else
						posStack(sp - 1) = pos
					End If
					db.pool.unfix(pg)
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

					Return DirectCast(db.lookupObject(oid, Nothing), V)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public Sub Reset() Implements IEnumerator.Reset
				db = DirectCast(tree.Database, DatabaseImpl)
				If db Is Nothing Then
					Throw New DatabaseException(Volante.DatabaseException.ErrorCode.DELETED_OBJECT)
				End If

				sp = 0
				Dim height As Integer = tree.height
				pageStack = New Integer(height - 1) {}
				posStack = New Integer(height - 1) {}
				updateCounter = tree.updateCounter
				Dim pageId As Integer = tree.root
				While System.Threading.Interlocked.Decrement(height) >= 0
					posStack(sp) = 0
					pageStack(sp) = pageId
					Dim pg As Page = db.getPage(pageId)
					pageId = getReference(pg, 0)
					[end] = OldBtreePage.getnItems(pg)
					db.pool.unfix(pg)
					sp += 1
				End While
				hasCurrent = False
			End Sub

			Protected db As DatabaseImpl
			Protected tree As OldBtree(Of K, V)
			Protected pageStack As Integer()
			Protected posStack As Integer()
			Protected sp As Integer
			Protected [end] As Integer
			Protected oid As Integer
			Protected hasCurrent As Boolean
			Protected updateCounter As Integer
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Private Class BtreeStrEnumerator
			Inherits BtreeEnumerator
			Friend Sub New(tree As OldBtree(Of K, V))
				MyBase.New(tree)
			End Sub

			Protected Overrides Function getReference(pg As Page, pos As Integer) As Integer
				Return OldBtreePage.getKeyStrOid(pg, pos)
			End Function
		End Class

		Private Class BtreeDictionaryEnumerator
			Inherits BtreeEnumerator
			Implements IDictionaryEnumerator
			Friend Sub New(tree As OldBtree(Of K, V))
				MyBase.New(tree)
			End Sub

			Protected Overrides Sub getCurrent(pg As Page, pos As Integer)
				oid = getReference(pg, pos)
				m_key = tree.unpackKey(db, pg, pos)
			End Sub

			Public Overridable Shadows ReadOnly Property Current() As Object
				Get
					Return Entry
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return New DictionaryEntry(m_key, db.lookupObject(oid, Nothing))
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

					Return db.lookupObject(oid, Nothing)
				End Get
			End Property

			Protected m_key As Object
		End Class

		Private Class BtreeDictionaryStrEnumerator
			Inherits BtreeDictionaryEnumerator
			Friend Sub New(tree As OldBtree(Of K, V))
				MyBase.New(tree)
			End Sub

			Protected Overrides Function getReference(pg As Page, pos As Integer) As Integer
				Return OldBtreePage.getKeyStrOid(pg, pos)
			End Function
		End Class

		Public Overridable Function GetDictionaryEnumerator() As IDictionaryEnumerator
			Return If(type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte, New BtreeDictionaryStrEnumerator(Me), New BtreeDictionaryEnumerator(Me))
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of V)
			Return If(type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte, New BtreeStrEnumerator(Me), New BtreeEnumerator(Me))
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Function compareByteArrays(key As Key, pg As Page, i As Integer) As Integer
			Return compareByteArrays(DirectCast(key.oval, Byte()), pg.data, OldBtreePage.getKeyStrOffs(pg, i) + OldBtreePage.firstKeyOffs, OldBtreePage.getKeyStrSize(pg, i))
		End Function

		Private Class BtreeSelectionIterator
			Implements IEnumerator(Of V)
			Implements IEnumerable(Of V)
			Friend Sub New(tree As OldBtree(Of K, V), from As Key, till As Key, order As IterationOrder)
				Me.from = from
				Me.till = till
				Me.type = tree.type
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
				Dim pg As Page
				Dim height As Integer = tree.height
				Dim pageId As Integer = tree.root
				updateCounter = tree.updateCounter
				hasCurrent = False
				sp = 0

				If height = 0 Then
					Return
				End If

				db = DirectCast(tree.Database, DatabaseImpl)
				If db Is Nothing Then
					Throw New DatabaseException(Volante.DatabaseException.ErrorCode.DELETED_OBJECT)
				End If

				pageStack = New Integer(height - 1) {}
				posStack = New Integer(height - 1) {}

				If type = ClassDescriptor.FieldType.tpString Then
					If order = IterationOrder.AscentOrder Then
						If from Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) >= 0
								posStack(sp) = 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								pageId = OldBtreePage.getKeyStrOid(pg, 0)
								[end] = OldBtreePage.getnItems(pg)
								db.pool.unfix(pg)
								sp += 1
							End While
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If OldBtreePage.compareStr(from, pg, i) >= from.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getKeyStrOid(pg, r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							[end] = InlineAssignHelper(r, OldBtreePage.getnItems(pg))
							While l < r
								i = (l + r) >> 1
								If OldBtreePage.compareStr(from, pg, i) >= from.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = [end] Then
								sp += 1
								gotoNextItem(pg, r - 1)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso till IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If -OldBtreePage.compareStr(till, pg, posStack(sp - 1)) >= till.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					Else
						' descent order
						If till Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								posStack(sp) = OldBtreePage.getnItems(pg)
								pageId = OldBtreePage.getKeyStrOid(pg, posStack(sp))
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = OldBtreePage.getnItems(pg) - 1
							db.pool.unfix(pg)
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If OldBtreePage.compareStr(till, pg, i) >= 1 - till.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getKeyStrOid(pg, r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							r = OldBtreePage.getnItems(pg)
							While l < r
								i = (l + r) >> 1
								If OldBtreePage.compareStr(till, pg, i) >= 1 - till.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = 0 Then
								sp += 1
								gotoNextItem(pg, r)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r - 1
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso from IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If OldBtreePage.compareStr(from, pg, posStack(sp - 1)) >= from.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					End If
				ElseIf type = ClassDescriptor.FieldType.tpArrayOfByte Then
					If order = IterationOrder.AscentOrder Then
						If from Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) >= 0
								posStack(sp) = 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								pageId = OldBtreePage.getKeyStrOid(pg, 0)
								[end] = OldBtreePage.getnItems(pg)
								db.pool.unfix(pg)
								sp += 1
							End While
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If tree.compareByteArrays(from, pg, i) >= from.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getKeyStrOid(pg, r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							[end] = InlineAssignHelper(r, OldBtreePage.getnItems(pg))
							While l < r
								i = (l + r) >> 1
								If tree.compareByteArrays(from, pg, i) >= from.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = [end] Then
								sp += 1
								gotoNextItem(pg, r - 1)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso till IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If -tree.compareByteArrays(till, pg, posStack(sp - 1)) >= till.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					Else
						' descent order
						If till Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								posStack(sp) = OldBtreePage.getnItems(pg)
								pageId = OldBtreePage.getKeyStrOid(pg, posStack(sp))
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = OldBtreePage.getnItems(pg) - 1
							db.pool.unfix(pg)
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If tree.compareByteArrays(till, pg, i) >= 1 - till.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getKeyStrOid(pg, r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							r = OldBtreePage.getnItems(pg)
							While l < r
								i = (l + r) >> 1
								If tree.compareByteArrays(till, pg, i) >= 1 - till.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = 0 Then
								sp += 1
								gotoNextItem(pg, r)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r - 1
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso from IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If tree.compareByteArrays(from, pg, posStack(sp - 1)) >= from.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					End If
				Else
					' scalar type
					If order = IterationOrder.AscentOrder Then
						If from Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) >= 0
								posStack(sp) = 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								pageId = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1)
								[end] = OldBtreePage.getnItems(pg)
								db.pool.unfix(pg)
								sp += 1
							End While
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If OldBtreePage.compare(from, pg, i) >= from.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							r = InlineAssignHelper([end], OldBtreePage.getnItems(pg))
							While l < r
								i = (l + r) >> 1
								If OldBtreePage.compare(from, pg, i) >= from.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = [end] Then
								sp += 1
								gotoNextItem(pg, r - 1)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso till IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If -OldBtreePage.compare(till, pg, posStack(sp - 1)) >= till.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					Else
						' descent order
						If till Is Nothing Then
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								posStack(sp) = OldBtreePage.getnItems(pg)
								pageId = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - posStack(sp))
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = OldBtreePage.getnItems(pg) - 1
							db.pool.unfix(pg)
						Else
							While System.Threading.Interlocked.Decrement(height) > 0
								pageStack(sp) = pageId
								pg = db.getPage(pageId)
								l = 0
								r = OldBtreePage.getnItems(pg)
								While l < r
									i = (l + r) >> 1
									If OldBtreePage.compare(till, pg, i) >= 1 - till.inclusion Then
										l = i + 1
									Else
										r = i
									End If
								End While
								Debug.Assert(r = l)
								posStack(sp) = r
								pageId = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - r)
								db.pool.unfix(pg)
								sp += 1
							End While
							pageStack(sp) = pageId
							pg = db.getPage(pageId)
							l = 0
							r = OldBtreePage.getnItems(pg)
							While l < r
								i = (l + r) >> 1
								If OldBtreePage.compare(till, pg, i) >= 1 - till.inclusion Then
									l = i + 1
								Else
									r = i
								End If
							End While
							Debug.Assert(r = l)
							If r = 0 Then
								sp += 1
								gotoNextItem(pg, r)
							Else
								posStack(System.Math.Max(System.Threading.Interlocked.Increment(sp),sp - 1)) = r - 1
								db.pool.unfix(pg)
							End If
						End If
						If sp <> 0 AndAlso from IsNot Nothing Then
							pg = db.getPage(pageStack(sp - 1))
							If OldBtreePage.compare(from, pg, posStack(sp - 1)) >= from.inclusion Then
								sp = 0
							End If
							db.pool.unfix(pg)
						End If
					End If
				End If
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If updateCounter <> tree.updateCounter Then
					Throw New InvalidOperationException("B-Tree was modified")
				End If

				If 0 = sp Then
					hasCurrent = False
					Return False
				End If

				Dim pos As Integer = posStack(sp - 1)
				Dim pg As Page = db.getPage(pageStack(sp - 1))
				hasCurrent = True
				getCurrent(pg, pos)
				gotoNextItem(pg, pos)
				Return True
			End Function

			Protected Overridable Sub getCurrent(pg As Page, pos As Integer)
				oid = If((type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte), OldBtreePage.getKeyStrOid(pg, pos), OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - pos))
			End Sub

			Public Overridable ReadOnly Property Current() As V Implements IEnumerator(Of V).Current
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return DirectCast(db.lookupObject(oid, Nothing), V)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Protected Sub gotoNextItem(pg As Page, pos As Integer)
				If type = ClassDescriptor.FieldType.tpString Then
					If order = IterationOrder.AscentOrder Then
						If System.Threading.Interlocked.Increment(pos) = [end] Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Increment(pos) <= OldBtreePage.getnItems(pg) Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getKeyStrOid(pg, pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										[end] = OldBtreePage.getnItems(pg)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, 0)
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso till IsNot Nothing AndAlso -OldBtreePage.compareStr(till, pg, pos) >= till.inclusion Then
							sp = 0
						End If
					Else
						' descent order
						If System.Threading.Interlocked.Decrement(pos) < 0 Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Decrement(pos) >= 0 Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getKeyStrOid(pg, pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, OldBtreePage.getnItems(pg))
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									posStack(sp - 1) = System.Threading.Interlocked.Decrement(pos)
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso from IsNot Nothing AndAlso OldBtreePage.compareStr(from, pg, pos) >= from.inclusion Then
							sp = 0
						End If
					End If
				ElseIf type = ClassDescriptor.FieldType.tpArrayOfByte Then
					If order = IterationOrder.AscentOrder Then
						If System.Threading.Interlocked.Increment(pos) = [end] Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Increment(pos) <= OldBtreePage.getnItems(pg) Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getKeyStrOid(pg, pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										[end] = OldBtreePage.getnItems(pg)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, 0)
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso till IsNot Nothing AndAlso -tree.compareByteArrays(till, pg, pos) >= till.inclusion Then
							sp = 0
						End If
					Else
						' descent order
						If System.Threading.Interlocked.Decrement(pos) < 0 Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Decrement(pos) >= 0 Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getKeyStrOid(pg, pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, OldBtreePage.getnItems(pg))
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									posStack(sp - 1) = System.Threading.Interlocked.Decrement(pos)
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso from IsNot Nothing AndAlso tree.compareByteArrays(from, pg, pos) >= from.inclusion Then
							sp = 0
						End If
					End If
				Else
					' scalar type
					If order = IterationOrder.AscentOrder Then
						If System.Threading.Interlocked.Increment(pos) = [end] Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Increment(pos) <= OldBtreePage.getnItems(pg) Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										[end] = OldBtreePage.getnItems(pg)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, 0)
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso till IsNot Nothing AndAlso -OldBtreePage.compare(till, pg, pos) >= till.inclusion Then
							sp = 0
						End If
					Else
						' descent order
						If System.Threading.Interlocked.Decrement(pos) < 0 Then
							While System.Threading.Interlocked.Decrement(sp) <> 0
								db.pool.unfix(pg)
								pos = posStack(sp - 1)
								pg = db.getPage(pageStack(sp - 1))
								If System.Threading.Interlocked.Decrement(pos) >= 0 Then
									posStack(sp - 1) = pos
									Do
										Dim pageId As Integer = OldBtreePage.getReference(pg, OldBtreePage.maxItems - 1 - pos)
										db.pool.unfix(pg)
										pg = db.getPage(pageId)
										pageStack(sp) = pageId
										posStack(sp) = InlineAssignHelper(pos, OldBtreePage.getnItems(pg))
									Loop While System.Threading.Interlocked.Increment(sp) < pageStack.Length
									posStack(sp - 1) = System.Threading.Interlocked.Decrement(pos)
									Exit While
								End If
							End While
						Else
							posStack(sp - 1) = pos
						End If
						If sp <> 0 AndAlso from IsNot Nothing AndAlso OldBtreePage.compare(from, pg, pos) >= from.inclusion Then
							sp = 0
						End If
					End If
				End If
				db.pool.unfix(pg)
			End Sub

			Protected db As DatabaseImpl
			Protected pageStack As Integer()
			Protected posStack As Integer()
			Protected tree As OldBtree(Of K, V)
			Protected sp As Integer
			Protected [end] As Integer
			Protected oid As Integer
			Protected from As Key
			Protected till As Key
			Protected hasCurrent As Boolean
			Protected order As IterationOrder
			Protected type As ClassDescriptor.FieldType
			Protected updateCounter As Integer
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Private Class BtreeDictionarySelectionIterator
			Inherits BtreeSelectionIterator
			Implements IDictionaryEnumerator
			Friend Sub New(tree As OldBtree(Of K, V), from As Key, till As Key, order As IterationOrder)
				MyBase.New(tree, from, till, order)
			End Sub

			Protected Overrides Sub getCurrent(pg As Page, pos As Integer)
				MyBase.getCurrent(pg, pos)
				m_key = tree.unpackKey(db, pg, pos)
			End Sub

			Public Overridable Shadows ReadOnly Property Current() As Object
				Get
					Return Entry
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public ReadOnly Property Entry() As DictionaryEntry Implements IDictionaryEnumerator.Entry
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If

					Return New DictionaryEntry(m_key, db.lookupObject(oid, Nothing))
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

					Return db.lookupObject(oid, Nothing)
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

		Public Function Reverse() As IEnumerable(Of V)
			Return New BtreeSelectionIterator(Me, Nothing, Nothing, IterationOrder.DescentOrder)
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

		Public Function StartsWith(prefix As String) As IEnumerable(Of V)
			Return Range(New Key(prefix.ToCharArray()), New Key((prefix & [Char].MaxValue).ToCharArray(), False), IterationOrder.AscentOrder)
		End Function

		Public Overridable Function GetDictionaryEnumerator(from As Key, till As Key, order As IterationOrder) As IDictionaryEnumerator
			Return New BtreeDictionarySelectionIterator(Me, checkKey(from), checkKey(till), order)
		End Function
	End Class
End Namespace
#End If
