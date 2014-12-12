#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Class OldBtreeFieldIndex(Of K, V As {Class, IPersistent})
		Inherits OldBtree(Of K, V)
		Implements IFieldIndex(Of K, V)
		Friend className As [String]
		Friend fieldName As [String]
		Friend autoincCount As Long
		<NonSerialized> _
		Private cls As Type
		<NonSerialized> _
		Private mbr As MemberInfo
		<NonSerialized> _
		Private mbrType As Type

		Friend Sub New()
		End Sub

		Private Sub lookupField(name As [String])
			Dim fld As FieldInfo = cls.GetField(fieldName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
			If fld Is Nothing Then
				Dim prop As PropertyInfo = cls.GetProperty(fieldName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
				If prop Is Nothing Then
					Throw New DatabaseException(DatabaseException.ErrorCode.INDEXED_FIELD_NOT_FOUND, className & "." & fieldName)
				End If
				mbrType = prop.PropertyType
				mbr = prop
			Else
				mbrType = fld.FieldType
				mbr = fld
			End If
			If mbrType IsNot GetType(K) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE, mbrType)
			End If
		End Sub

		Public ReadOnly Property IndexedClass() As Type
			Get
				Return cls
			End Get
		End Property

		Public ReadOnly Property KeyField() As MemberInfo
			Get
				Return mbr
			End Get
		End Property

		Public Overrides Sub OnLoad()
			cls = ClassDescriptor.lookup(Database, className)
			If cls IsNot GetType(V) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls)
			End If

			lookupField(fieldName)
		End Sub

		Friend Sub New(fieldName As [String], unique As Boolean)
			Me.New(fieldName, unique, 0)
		End Sub

		Friend Sub New(fieldName As [String], unique As Boolean, autoincCount As Long)
			Me.New(GetType(V), fieldName, unique, autoincCount)
		End Sub

		Friend Sub New(cls As Type, fieldName As String, unique As Boolean, autoincCount As Long)
			init(cls, ClassDescriptor.FieldType.tpLast, New String() {fieldName}, unique, autoincCount)
		End Sub

		Public Overrides Sub init(cls As Type, type As ClassDescriptor.FieldType, fieldNames As String(), unique As Boolean, autoincCount As Long)
			Me.cls = cls
			Me.unique = unique
			Me.fieldName = fieldNames(0)
			Me.className = ClassDescriptor.getTypeName(cls)
			Me.autoincCount = autoincCount
			lookupField(fieldNames(0))
			Me.type = checkType(mbrType)
		End Sub

		Protected Overrides Function unpackEnum(val As Integer) As Object
			Return [Enum].ToObject(mbrType, val)
		End Function

		Private Function extractKey(obj As IPersistent) As Key
			Dim val As [Object] = If(TypeOf mbr Is FieldInfo, DirectCast(mbr, FieldInfo).GetValue(obj), DirectCast(mbr, PropertyInfo).GetValue(obj, Nothing))
			Dim key As Key = Nothing
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					key = New Key(CBool(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpByte
					key = New Key(CByte(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpSByte
					key = New Key(CSByte(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpShort
					key = New Key(CShort(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpUShort
					key = New Key(CUShort(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpChar
					key = New Key(CChar(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpInt
					key = New Key(CInt(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpUInt
					key = New Key(CUInt(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpOid
					key = New Key(ClassDescriptor.FieldType.tpOid, CInt(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpObject
					key = New Key(DirectCast(val, IPersistent))
					Exit Select
				Case ClassDescriptor.FieldType.tpLong
					key = New Key(CLng(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpULong
					key = New Key(CULng(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpDate
					key = New Key(CType(val, DateTime))
					Exit Select
				Case ClassDescriptor.FieldType.tpFloat
					key = New Key(CSng(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpDouble
					key = New Key(CDbl(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpDecimal
					key = New Key(CDec(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpGuid
					key = New Key(CType(val, Guid))
					Exit Select
				Case ClassDescriptor.FieldType.tpString
					key = New Key(DirectCast(val, String).ToCharArray())
					Exit Select
				Case ClassDescriptor.FieldType.tpEnum
					key = New Key(DirectCast(val, [Enum]))
					Exit Select
				Case Else
					Debug.Assert(False, "Invalid type")
					Exit Select
			End Select
			Return key
		End Function

		Public Function Put(obj As V) As Boolean
			Return MyBase.Put(extractKey(obj), obj)
		End Function

		Public Function [Set](obj As V) As V
			Return MyBase.[Set](extractKey(obj), obj)
		End Function

		Public Overrides Function Remove(obj As V) As Boolean
			Try
				MyBase.remove(New OldBtreeKey(extractKey(obj), obj.Oid))
			Catch x As DatabaseException
				If x.Code = DatabaseException.ErrorCode.KEY_NOT_FOUND Then
					Return False
				End If
				Throw
			End Try
			Return True
		End Function

		Public Overrides Function Contains(obj As V) As Boolean
			Dim key As Key = extractKey(obj)
			If unique Then
				Return MyBase.[Get](key) = obj
			End If

			Dim mbrs As V() = [Get](key, key)
			For i As Integer = 0 To mbrs.Length - 1
				If mbrs(i) = obj Then
					Return True
				End If
			Next
			Return False
		End Function

		Public Sub Append(obj As V)
			SyncLock Me
				Dim key As Key
				Dim val As Object
				Select Case type
					Case ClassDescriptor.FieldType.tpInt
						key = New Key(CInt(autoincCount))
						val = CInt(autoincCount)
						Exit Select
					Case ClassDescriptor.FieldType.tpLong
						key = New Key(autoincCount)
						val = autoincCount
						Exit Select
					Case Else
						Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE, mbrType)
				End Select
				If TypeOf mbr Is FieldInfo Then
					DirectCast(mbr, FieldInfo).SetValue(obj, val)
				Else
					DirectCast(mbr, PropertyInfo).SetValue(obj, val, Nothing)
				End If
				autoincCount += 1
				obj.Modify()
				MyBase.insert(key, obj, False)
			End SyncLock
		End Sub

		Public Overrides Function [Get](from As Key, till As Key) As V()
			Dim list As New ArrayList()
			If root <> 0 Then
				OldBtreePage.find(DirectCast(Database, DatabaseImpl), root, checkKey(from), checkKey(till), Me, height, _
					list)
			End If
			Return DirectCast(list.ToArray(cls), V())
		End Function

		Public Overrides Function ToArray() As V()
			Dim arr As V() = DirectCast(Array.CreateInstance(cls, nElems), V())
			If root <> 0 Then
				OldBtreePage.traverseForward(DirectCast(Database, DatabaseImpl), root, type, height, arr, 0)
			End If
			Return arr
		End Function
	End Class
End Namespace
#End If
