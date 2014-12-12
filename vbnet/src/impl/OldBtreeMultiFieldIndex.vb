#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Class OldBtreeMultiFieldIndex(Of V As {Class, IPersistent})
		Inherits OldBtree(Of Object(), V)
		Implements IMultiFieldIndex(Of V)
		Friend className As [String]
		Friend fieldNames As [String]()
		Friend types As ClassDescriptor.FieldType()
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

		Public Overrides ReadOnly Property FieldTypes() As ClassDescriptor.FieldType()
			Get
				Return types
			End Get
		End Property

		Public Overrides Sub OnLoad()
			cls = ClassDescriptor.lookup(Database, className)
			If cls IsNot GetType(V) Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_VALUE_TYPE, cls)
			End If
			locateFields()
		End Sub

		Friend Sub New(fieldNames As String(), unique As Boolean)
			Me.New(GetType(V), fieldNames, unique)
		End Sub

		Friend Sub New(cls As Type, fieldNames As String(), unique As Boolean)
			init(cls, ClassDescriptor.FieldType.tpLast, fieldNames, unique, 0)
		End Sub

		Public Overrides Sub init(cls As Type, type As ClassDescriptor.FieldType, fieldNames As String(), unique As Boolean, autoincCount As Long)
			Me.cls = cls
			Me.unique = unique
			Me.fieldNames = fieldNames
			Me.className = ClassDescriptor.getTypeName(cls)
			locateFields()
			Me.type = ClassDescriptor.FieldType.tpArrayOfByte
			types = New ClassDescriptor.FieldType(fieldNames.Length - 1) {}
			For i As Integer = 0 To types.Length - 1
				Dim mbrType As Type = If(TypeOf mbr(i) Is FieldInfo, DirectCast(mbr(i), FieldInfo).FieldType, DirectCast(mbr(i), PropertyInfo).PropertyType)
				types(i) = checkType(mbrType)
			Next
		End Sub

		Public Overrides Function compareByteArrays(key As Byte(), item As Byte(), offs As Integer, lengtn As Integer) As Integer
			Dim o1 As Integer = 0
			Dim o2 As Integer = offs
			Dim a1 As Byte() = key
			Dim a2 As Byte() = item
			Dim i As Integer = 0
			While i < types.Length AndAlso o1 < key.Length
				Dim diff As Integer = 0
				Select Case types(i)
					Case ClassDescriptor.FieldType.tpBoolean, ClassDescriptor.FieldType.tpByte
						diff = a1(System.Math.Max(System.Threading.Interlocked.Increment(o1),o1 - 1)) - a2(System.Math.Max(System.Threading.Interlocked.Increment(o2),o2 - 1))
						Exit Select
					Case ClassDescriptor.FieldType.tpSByte
						diff = CSByte(a1(System.Math.Max(System.Threading.Interlocked.Increment(o1),o1 - 1))) - CSByte(a2(System.Math.Max(System.Threading.Interlocked.Increment(o2),o2 - 1)))
						Exit Select
					Case ClassDescriptor.FieldType.tpShort
						diff = Bytes.unpack2(a1, o1) - Bytes.unpack2(a2, o2)
						o1 += 2
						o2 += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpUShort
						diff = CUShort(Bytes.unpack2(a1, o1)) - CUShort(Bytes.unpack2(a2, o2))
						o1 += 2
						o2 += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpChar
						diff = CChar(Bytes.unpack2(a1, o1)) - CChar(Bytes.unpack2(a2, o2))
						o1 += 2
						o2 += 2
						Exit Select
					Case ClassDescriptor.FieldType.tpInt
						If True Then
							Dim i1 As Integer = Bytes.unpack4(a1, o1)
							Dim i2 As Integer = Bytes.unpack4(a2, o2)
							diff = If(i1 < i2, -1, If(i1 = i2, 0, 1))
							o1 += 4
							o2 += 4
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
						If True Then
							Dim u1 As UInteger = CUInt(Bytes.unpack4(a1, o1))
							Dim u2 As UInteger = CUInt(Bytes.unpack4(a2, o2))
							diff = If(u1 < u2, -1, If(u1 = u2, 0, 1))
							o1 += 4
							o2 += 4
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpLong
						If True Then
							Dim l1 As Long = Bytes.unpack8(a1, o1)
							Dim l2 As Long = Bytes.unpack8(a2, o2)
							diff = If(l1 < l2, -1, If(l1 = l2, 0, 1))
							o1 += 8
							o2 += 8
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpULong, ClassDescriptor.FieldType.tpDate
						If True Then
							Dim l1 As ULong = CULng(Bytes.unpack8(a1, o1))
							Dim l2 As ULong = CULng(Bytes.unpack8(a2, o2))
							diff = If(l1 < l2, -1, If(l1 = l2, 0, 1))
							o1 += 8
							o2 += 8
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpFloat
						If True Then
							Dim f1 As Single = Bytes.unpackF4(a1, o1)
							Dim f2 As Single = Bytes.unpackF4(a2, o2)
							diff = If(f1 < f2, -1, If(f1 = f2, 0, 1))
							o1 += 4
							o2 += 4
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpDouble
						If True Then
							Dim d1 As Double = Bytes.unpackF8(a1, o1)
							Dim d2 As Double = Bytes.unpackF8(a2, o2)
							diff = If(d1 < d2, -1, If(d1 = d2, 0, 1))
							o1 += 8
							o2 += 8
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpDecimal
						If True Then
							Dim d1 As Decimal = Bytes.unpackDecimal(a1, o1)
							Dim d2 As Decimal = Bytes.unpackDecimal(a2, o2)
							diff = d1.CompareTo(d2)
							o1 += 16
							o2 += 16
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpGuid
						If True Then
							Dim g1 As Guid = Bytes.unpackGuid(a1, o1)
							Dim g2 As Guid = Bytes.unpackGuid(a2, o2)
							diff = g1.CompareTo(g2)
							o1 += 16
							o2 += 16
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpString
						If True Then
							Dim s1 As String, s2 As String
							o1 = Bytes.unpackString(a1, o1, s1)
							o2 = Bytes.unpackString(a2, o2, s2)
							diff = [String].CompareOrdinal(s1, s2)
							' TODO: old version, remove
'                            int len1 = Bytes.unpack4(a1, o1);
'                            int len2 = Bytes.unpack4(a2, o2);
'                            o1 += 4;
'                            o2 += 4;
'                            int len = len1 < len2 ? len1 : len2;
'                            while (--len >= 0)
'                            {
'                                diff = (char)Bytes.unpack2(a1, o1) - (char)Bytes.unpack2(a2, o2);
'                                if (diff != 0)
'                                {
'                                    return diff;
'                                }
'                                o1 += 2;
'                                o2 += 2;
'                            }
'                            diff = len1 - len2;
'                             

							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpArrayOfByte
						If True Then
							Dim len1 As Integer = Bytes.unpack4(a1, o1)
							Dim len2 As Integer = Bytes.unpack4(a2, o2)
							o1 += 4
							o2 += 4
							Dim len As Integer = If(len1 < len2, len1, len2)
							While System.Threading.Interlocked.Decrement(len) >= 0
								diff = a1(System.Math.Max(System.Threading.Interlocked.Increment(o1),o1 - 1)) - a2(System.Math.Max(System.Threading.Interlocked.Increment(o2),o2 - 1))
								If diff <> 0 Then
									Return diff
								End If
							End While
							diff = len1 - len2
							Exit Select
						End If
					Case Else
						Debug.Assert(False, "Invalid type")
						Exit Select
				End Select
				If diff <> 0 Then
					Return diff
				End If
				i += 1
			End While
			Return 0
		End Function

		Protected Overrides Function unpackByteArrayKey(pg As Page, pos As Integer) As Object
			Dim offs As Integer = OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, pos)
			Dim data As Byte() = pg.data
			Dim values As [Object]() = New [Object](types.Length - 1) {}

			For i As Integer = 0 To types.Length - 1
				Dim v As [Object] = Nothing
				Select Case types(i)
					Case ClassDescriptor.FieldType.tpBoolean
						v = data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0
						Exit Select

					Case ClassDescriptor.FieldType.tpSByte
						v = CSByte(data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))
						Exit Select

					Case ClassDescriptor.FieldType.tpByte
						v = data(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
						Exit Select

					Case ClassDescriptor.FieldType.tpShort
						v = Bytes.unpack2(data, offs)
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpUShort
						v = CUShort(Bytes.unpack2(data, offs))
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpChar
						v = CChar(Bytes.unpack2(data, offs))
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpInt
						v = Bytes.unpack4(data, offs)
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpEnum
						v = [Enum].ToObject(If(TypeOf mbr(i) Is FieldInfo, DirectCast(mbr(i), FieldInfo).FieldType, DirectCast(mbr(i), PropertyInfo).PropertyType), Bytes.unpack4(data, offs))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpUInt
						v = CUInt(Bytes.unpack4(data, offs))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpObject
						If True Then
							Dim oid As Integer = Bytes.unpack4(data, offs)
							v = If(oid = 0, Nothing, DirectCast(Database, DatabaseImpl).lookupObject(oid, Nothing))
							offs += 4
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpLong
						v = Bytes.unpack8(data, offs)
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpDate
						If True Then
							v = New DateTime(Bytes.unpack8(data, offs))
							offs += 8
							Exit Select
						End If
					Case ClassDescriptor.FieldType.tpULong
						v = CULng(Bytes.unpack8(data, offs))
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpFloat
						v = Bytes.unpackF4(data, offs)
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpDouble
						v = Bytes.unpackF8(data, offs)
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpDecimal
						v = Bytes.unpackDecimal(data, offs)
						offs += 16
						Exit Select

					Case ClassDescriptor.FieldType.tpGuid
						v = Bytes.unpackGuid(data, offs)
						offs += 16
						Exit Select

					Case ClassDescriptor.FieldType.tpString
						If True Then
							Dim len As Integer = Bytes.unpack4(data, offs)
							offs += 4
							Dim sval As Char() = New Char(len - 1) {}
							For j As Integer = 0 To len - 1
								sval(j) = CChar(Bytes.unpack2(pg.data, offs))
								offs += 2
							Next
							v = New [String](sval)
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfByte
						If True Then
							Dim len As Integer = Bytes.unpack4(data, offs)
							offs += 4
							Dim val As Byte() = New Byte(len - 1) {}
							Array.Copy(pg.data, offs, val, 0, len)
							offs += len
							v = val
							Exit Select
						End If
					Case Else
						Debug.Assert(False, "Invalid type")
						Exit Select
				End Select
				values(i) = v
			Next
			Return values
		End Function


		Private Function extractKey(obj As IPersistent) As Key
			Dim buf As New ByteBuffer()
			Dim dst As Integer = 0
			For i As Integer = 0 To types.Length - 1
				Dim val As Object = If(TypeOf mbr(i) Is FieldInfo, DirectCast(mbr(i), FieldInfo).GetValue(obj), DirectCast(mbr(i), PropertyInfo).GetValue(obj, Nothing))
				dst = packKeyPart(buf, dst, types(i), val)
			Next
			Return New Key(buf.toArray())
		End Function

		Private Function convertKey(key As Key) As Key
			If key Is Nothing Then
				Return Nothing
			End If

			If key.type <> ClassDescriptor.FieldType.tpArrayOfObject Then
				Throw New DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)
			End If

			Dim values As [Object]() = DirectCast(key.oval, [Object]())
			Dim buf As New ByteBuffer()
			Dim dst As Integer = 0

			For i As Integer = 0 To values.Length - 1
				dst = packKeyPart(buf, dst, types(i), values(i))
			Next
			Return New Key(buf.toArray(), key.inclusion <> 0)
		End Function

		Private Function packKeyPart(buf As ByteBuffer, dst As Integer, type As ClassDescriptor.FieldType, val As Object) As Integer
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					dst = buf.packBool(dst, CBool(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpByte
					dst = buf.packI1(dst, CByte(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpSByte
					dst = buf.packI1(dst, CSByte(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpShort
					dst = buf.packI2(dst, CShort(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpUShort
					dst = buf.packI2(dst, CUShort(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpChar
					dst = buf.packI2(dst, CChar(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpEnum
					dst = buf.packI4(dst, CInt(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpUInt
					dst = buf.packI4(dst, CInt(CUInt(val)))
					Exit Select
				Case ClassDescriptor.FieldType.tpObject
					dst = buf.packI4(dst, If(val IsNot Nothing, CInt(DirectCast(val, IPersistent).Oid), 0))
					Exit Select
				Case ClassDescriptor.FieldType.tpLong
					dst = buf.packI8(dst, CLng(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpULong
					dst = buf.packI8(dst, CLng(CULng(val)))
					Exit Select
				Case ClassDescriptor.FieldType.tpDate
					dst = buf.packDate(dst, CType(val, DateTime))
					Exit Select
				Case ClassDescriptor.FieldType.tpFloat
					dst = buf.packF4(dst, CSng(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpDouble
					dst = buf.packF8(dst, CDbl(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpDecimal
					dst = buf.packDecimal(dst, CDec(val))
					Exit Select
				Case ClassDescriptor.FieldType.tpGuid
					dst = buf.packGuid(dst, CType(val, Guid))
					Exit Select
				Case ClassDescriptor.FieldType.tpString
					dst = buf.packString(dst, DirectCast(val, String))
					Exit Select
				Case ClassDescriptor.FieldType.tpArrayOfByte
					buf.extend(dst + 4)
					If val IsNot Nothing Then
						Dim arr As Byte() = DirectCast(val, Byte())
						Dim len As Integer = arr.Length
						Bytes.pack4(buf.arr, dst, len)
						dst += 4
						buf.extend(dst + len)
						Array.Copy(arr, 0, buf.arr, dst, len)
						dst += len
					Else
						Bytes.pack4(buf.arr, dst, 0)
						dst += 4
					End If
					Exit Select
				Case Else
					Debug.Assert(False, "Invalid type")
					Exit Select
			End Select
			Return dst
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

		Public Overrides Function Remove(key As Key) As V
			Return MyBase.Remove(convertKey(key))
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
			Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE)
		End Sub

		Public Overrides Function [Get](key As Key) As V
			Return MyBase.[Get](convertKey(key))
		End Function

		Public Overrides Function Range(from As Key, till As Key, order As IterationOrder) As IEnumerable(Of V)
			Return MyBase.Range(convertKey(from), convertKey(till), order)
		End Function


		Public Overrides Function GetDictionaryEnumerator(from As Key, till As Key, order As IterationOrder) As IDictionaryEnumerator
			Return MyBase.GetDictionaryEnumerator(convertKey(from), convertKey(till), order)
		End Function
	End Class
End Namespace
#End If
