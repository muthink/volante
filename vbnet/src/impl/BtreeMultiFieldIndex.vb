Imports System.Collections.Generic
Imports System.Collections
Imports System.Reflection
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Class BtreeMultiFieldIndex(Of T As {Class, IPersistent})
		Inherits Btree(Of Object(), T)
		Implements IMultiFieldIndex(Of T)
		Friend className As [String]
		Friend fieldNames As [String]()
		<NonSerialized> _
		Private cls As Type
		<NonSerialized> _
		Private mbr As MemberInfo()

		Friend Sub New()
		End Sub

		Private Sub locateFields()
			mbr = New MemberInfo(fieldNames.Length - 1) {}
			For i As Integer = 0 To fieldNames.Length - 1
				mbr(i) = cls.GetField(fieldNames(i), BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
				If mbr(i) Is Nothing Then
					mbr(i) = cls.GetProperty(fieldNames(i), BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
					If mbr(i) Is Nothing Then
						Throw New DatabaseException(DatabaseException.ErrorCode.INDEXED_FIELD_NOT_FOUND, className & "." & fieldNames(i))
					End If
				End If
			Next
		End Sub

		Public ReadOnly Property IndexedClass() As Type
			Get
				Return cls
			End Get
		End Property

		Public ReadOnly Property KeyField() As MemberInfo
			Get
				Return mbr(0)
			End Get
		End Property

		Public ReadOnly Property KeyFields() As MemberInfo()
			Get
				Return mbr
			End Get
		End Property

		Public Overrides Sub OnLoad()
			cls = ClassDescriptor.lookup(Database, className)
			If cls IsNot GetType(T) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls)
			End If

			locateFields()
		End Sub

		Friend Sub New(fieldNames As String(), unique As Boolean)
			Me.cls = GetType(T)
			Me.unique = unique
			Me.fieldNames = fieldNames
			Me.className = ClassDescriptor.getTypeName(cls)
			locateFields()
			type = ClassDescriptor.FieldType.tpRaw
		End Sub

		<Serializable> _
		Friend Class CompoundKey
			Implements IComparable
			Friend keys As Object()

			Public Function CompareTo(o As Object) As Integer Implements IComparable.CompareTo
				Dim c As CompoundKey = DirectCast(o, CompoundKey)
				Dim n As Integer = If(keys.Length < c.keys.Length, keys.Length, c.keys.Length)
				For i As Integer = 0 To n - 1
					Dim diff As Integer = DirectCast(keys(i), IComparable).CompareTo(c.keys(i))
					If diff <> 0 Then
						Return diff
					End If
				Next
				Return keys.Length - c.keys.Length
			End Function

			Friend Sub New(keys As Object())
				Me.keys = keys
			End Sub
		End Class

		Private Function convertKey(key As Key) As Key
			If key Is Nothing Then
				Return Nothing
			End If

			If key.type <> ClassDescriptor.FieldType.tpArrayOfObject Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If

			Return New Key(New CompoundKey(DirectCast(key.oval, System.Object())), key.inclusion <> 0)
		End Function

		Private Function extractKey(obj As IPersistent) As Key
			Dim keys As Object() = New Object(mbr.Length - 1) {}
			For i As Integer = 0 To keys.Length - 1
				keys(i) = If(TypeOf mbr(i) Is FieldInfo, DirectCast(mbr(i), FieldInfo).GetValue(obj), DirectCast(mbr(i), PropertyInfo).GetValue(obj, Nothing))
			Next
			Return New Key(New CompoundKey(keys))
		End Function

		Public Function Put(obj As T) As Boolean
			Return MyBase.Put(extractKey(obj), obj)
		End Function

		Public Function [Set](obj As T) As T
			Return MyBase.[Set](extractKey(obj), obj)
		End Function

		Public Overrides Function Remove(obj As T) As Boolean
			Try
				MyBase.Remove(New BtreeKey(extractKey(obj), obj))
			Catch x As DatabaseException
				If x.Code = DatabaseException.ErrorCode.KEY_NOT_FOUND Then
					Return False
				End If

				Throw
			End Try
			Return True
		End Function

		Public Overrides Function Remove(key As Key) As T
			Return MyBase.Remove(convertKey(key))
		End Function


		Public Overrides Function Contains(obj As T) As Boolean
			Dim key As Key = extractKey(obj)
			If unique Then
				Return MyBase.[Get](key) = obj
			End If

			Dim mbrs As T() = GetNoKeyConvert(key, key)

			For i As Integer = 0 To mbrs.Length - 1
				If mbrs(i) = obj Then
					Return True
				End If
			Next
			Return False
		End Function

		Public Sub Append(obj As T)
			Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE)
		End Sub

		Private Function GetNoKeyConvert(from As Key, till As Key) As T()
			Dim list As New ArrayList()
			If root IsNot Nothing Then
				root.find(from, till, height, list)
			End If
			Return DirectCast(list.ToArray(cls), T())
		End Function

		Public Overrides Function [Get](from As Key, till As Key) As T()
			Dim list As New ArrayList()
			If root IsNot Nothing Then
				root.find(convertKey(from), convertKey(till), height, list)
			End If
			Return DirectCast(list.ToArray(cls), T())
		End Function

		Public Overrides Function ToArray() As T()
			Dim arr As T() = New T(nElems - 1) {}
			If root IsNot Nothing Then
				root.traverseForward(height, arr, 0)
			End If
			Return arr
		End Function

		Public Overrides Function [Get](key As Key) As T
			Return MyBase.[Get](convertKey(key))
		End Function

		Public Overrides Function Range(from As Key, till As Key, order As IterationOrder) As IEnumerable(Of T)
			Return MyBase.Range(convertKey(from), convertKey(till), order)
		End Function

		Public Overrides Function GetDictionaryEnumerator(from As Key, till As Key, order As IterationOrder) As IDictionaryEnumerator
			Return MyBase.GetDictionaryEnumerator(convertKey(from), convertKey(till), order)
		End Function
	End Class
End Namespace
