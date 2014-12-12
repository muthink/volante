#If WITH_XML Then
Imports System.Collections
Imports System.Reflection
Imports Volante
Namespace Volante.Impl

	Public Class XmlImporter
		Public Sub New(db As DatabaseImpl, reader As System.IO.StreamReader)
			Me.db = db
			scanner = New XMLScanner(reader)
			classMap = New Hashtable()
		End Sub

		Public Overridable Sub importDatabase()
			If scanner.scan() <> XMLScanner.Token.LT OrElse scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals("database") Then
				throwException("No root element")
			End If
			If scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals("root") OrElse scanner.scan() <> XMLScanner.Token.EQ OrElse scanner.scan() <> XMLScanner.Token.SCONST OrElse scanner.scan() <> XMLScanner.Token.GT Then
				throwException("Database element should have ""root"" attribute")
			End If
			Dim rootId As Integer = 0
			Try
				rootId = System.Int32.Parse(scanner.[String])
			Catch generatedExceptionName As System.FormatException
				throwException("Incorrect root object specification")
			End Try
			idMap = New Integer(rootId * 2 - 1) {}
			idMap(rootId) = db.allocateId()
			db.header.root(1 - db.currIndex).rootObject = idMap(rootId)

			Dim tkn As XMLScanner.Token
			While (InlineAssignHelper(tkn, scanner.scan())) = XMLScanner.Token.LT
				If scanner.scan() <> XMLScanner.Token.IDENT Then
					throwException("Element name expected")
				End If
				Dim elemName As System.String = scanner.Identifier
				If elemName.StartsWith("Volante.Impl.OldBtree") OrElse elemName.StartsWith("Volante.Impl.OldBitIndexImpl") OrElse elemName.StartsWith("Volante.Impl.OldPersistentSet") OrElse elemName.StartsWith("Volante.Impl.OldBtreeFieldIndex") OrElse elemName.StartsWith("Volante.Impl.OldBtreeMultiFieldIndex") Then
					createIndex(elemName)
				Else
					createObject(readElement(elemName))
				End If
			End While
			If tkn <> XMLScanner.Token.LTS OrElse scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals("database") OrElse scanner.scan() <> XMLScanner.Token.GT Then
				throwException("Root element is not closed")
			End If
		End Sub

		Friend Class XMLElement
			Friend ReadOnly Property NextSibling() As XMLElement
				Get
					Return [next]
				End Get
			End Property

			Friend ReadOnly Property Counter() As Integer
				Get
					Return m_counter
				End Get
			End Property

			Friend Property IntValue() As Long
				Get
					Return ivalue
				End Get

				Set
					ivalue = value
					valueType = XMLValueType.INT_VALUE
				End Set
			End Property

			Friend Property RealValue() As Double
				Get
					Return rvalue
				End Get

				Set
					rvalue = value
					valueType = XMLValueType.REAL_VALUE
				End Set
			End Property

			Friend Property StringValue() As [String]
				Get
					Return svalue
				End Get

				Set
					svalue = value
					valueType = XMLValueType.STRING_VALUE
				End Set
			End Property


			Friend ReadOnly Property Name() As [String]
				Get

					Return m_name
				End Get
			End Property

			Private [next] As XMLElement
			Private prev As XMLElement
			Private m_name As [String]
			Private siblings As Hashtable
			Private attributes As Hashtable
			Private svalue As [String]
			Private ivalue As Long
			Private rvalue As Double
			Private valueType As XMLValueType
			Private m_counter As Integer

			Private Enum XMLValueType
				NO_VALUE
				STRING_VALUE
				INT_VALUE
				REAL_VALUE
				NULL_VALUE
			End Enum

			Friend Sub New(name As System.String)
				Me.m_name = name
				valueType = XMLValueType.NO_VALUE
			End Sub

			Friend Sub addSibling(elem As XMLElement)
				If siblings Is Nothing Then
					siblings = New Hashtable()
				End If
				Dim head As XMLElement = DirectCast(siblings(elem.name), XMLElement)
				If head IsNot Nothing Then
					elem.[next] = Nothing
					elem.prev = head.prev
					head.prev.[next] = elem
					head.prev = elem
					head.counter += 1
				Else
					elem.prev = elem
					siblings(elem.name) = elem
					elem.counter = 1
				End If
			End Sub

			Friend Sub addAttribute(name As System.String, val As System.String)
				If attributes Is Nothing Then
					attributes = New Hashtable()
				End If
				attributes(name) = val
			End Sub

			Friend Function getSibling(name As System.String) As XMLElement
				If siblings IsNot Nothing Then
					Return DirectCast(siblings(name), XMLElement)
				End If
				Return Nothing
			End Function

			Friend Function getAttribute(name As System.String) As System.String
				Return If(attributes IsNot Nothing, DirectCast(attributes(name), System.String), Nothing)
			End Function

			Friend Sub setNullValue()
				valueType = XMLValueType.NULL_VALUE
			End Sub

			Friend Function isIntValue() As Boolean
				Return valueType = XMLValueType.INT_VALUE
			End Function

			Friend Function isRealValue() As Boolean
				Return valueType = XMLValueType.REAL_VALUE
			End Function

			Friend Function isStringValue() As Boolean
				Return valueType = XMLValueType.STRING_VALUE
			End Function

			Friend Function isNullValue() As Boolean
				Return valueType = XMLValueType.NULL_VALUE
			End Function
		End Class

		Friend Function getAttribute(elem As XMLElement, name As [String]) As System.String
			Dim val As System.String = elem.getAttribute(name)
			If val Is Nothing Then
				throwException("Attribute " & name & " is not set")
			End If
			Return val
		End Function

		Friend Function getIntAttribute(elem As XMLElement, name As [String]) As Integer
			Dim val As System.String = elem.getAttribute(name)
			If val Is Nothing Then
				throwException("Attribute " & name & " is not set")
			End If
			Try
				Return System.Int32.Parse(val)
			Catch generatedExceptionName As System.FormatException
				throwException("Attribute " & name & " should has integer value")
			End Try
			Return -1
		End Function

		Friend Function mapId(id As Integer) As Integer
			Dim oid As Integer = 0
			If id <> 0 Then
				If id >= idMap.Length Then
					Dim newMap As Integer() = New Integer(id * 2 - 1) {}
					Array.Copy(idMap, 0, newMap, 0, idMap.Length)
					idMap = newMap
					idMap(id) = InlineAssignHelper(oid, db.allocateId())
				Else
					oid = idMap(id)
					If oid = 0 Then
						idMap(id) = InlineAssignHelper(oid, db.allocateId())
					End If
				End If
			End If
			Return oid
		End Function

		Friend Function mapType(signature As System.String) As ClassDescriptor.FieldType
			Try
				#If CF Then
				Return DirectCast(ClassDescriptor.parseEnum(GetType(ClassDescriptor.FieldType), signature), ClassDescriptor.FieldType)
				#Else
					#End If
				Return DirectCast([Enum].Parse(GetType(ClassDescriptor.FieldType), signature), ClassDescriptor.FieldType)
			Catch generatedExceptionName As ArgumentException
				throwException("Bad type")
				Return ClassDescriptor.FieldType.tpObject
			End Try
		End Function

		Private Function createCompoundKey(types As ClassDescriptor.FieldType(), values As [String]()) As Key
			Dim buf As New ByteBuffer()
			Dim dst As Integer = 0

			For i As Integer = 0 To types.Length - 1
				Dim val As [String] = values(i)
				Select Case types(i)
					Case ClassDescriptor.FieldType.tpBoolean
						dst = buf.packBool(dst, Int32.Parse(val) <> 0)
						Exit Select

					Case ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte
						dst = buf.packI1(dst, Int32.Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpChar, ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort
						dst = buf.packI2(dst, Int32.Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpInt
						dst = buf.packI4(dst, Int32.Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpUInt
						dst = buf.packI4(dst, CInt(UInt32.Parse(val)))
						Exit Select

					Case ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
						dst = buf.packI4(dst, mapId(CInt(UInt32.Parse(val))))
						Exit Select

					Case ClassDescriptor.FieldType.tpLong
						dst = buf.packI8(dst, Int64.Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpULong
						dst = buf.packI8(dst, CLng(UInt64.Parse(val)))
						Exit Select

					Case ClassDescriptor.FieldType.tpDate
						dst = buf.packDate(dst, DateTime.Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpFloat
						dst = buf.packF4(dst, [Single].Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpDouble
						dst = buf.packF8(dst, [Double].Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpDecimal
						dst = buf.packDecimal(dst, [Decimal].Parse(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpGuid
						dst = buf.packGuid(dst, New Guid(val))
						Exit Select

					Case ClassDescriptor.FieldType.tpString
						dst = buf.packString(dst, val)
						Exit Select

					Case ClassDescriptor.FieldType.tpArrayOfByte
						buf.extend(dst + 4 + (val.Length >> 1))
						Bytes.pack4(buf.arr, dst, val.Length >> 1)
						dst += 4
						Dim j As Integer = 0, n As Integer = val.Length
						While j < n
							buf.arr(System.Math.Max(System.Threading.Interlocked.Increment(dst),dst - 1)) = CByte((getHexValue(val(j)) << 4) Or getHexValue(val(j + 1)))
							j += 2
						End While
						Exit Select
					Case Else
						throwException("Bad key type")
						Exit Select
				End Select
			Next
			Return New Key(buf.toArray())
		End Function

		Private Function createKey(type As ClassDescriptor.FieldType, val As [String]) As Key
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					Return New Key(Int32.Parse(val) <> 0)

				Case ClassDescriptor.FieldType.tpByte
					Return New Key([Byte].Parse(val))

				Case ClassDescriptor.FieldType.tpSByte
					Return New Key([SByte].Parse(val))

				Case ClassDescriptor.FieldType.tpChar
					Return New Key(ChrW(Int32.Parse(val)))

				Case ClassDescriptor.FieldType.tpShort
					Return New Key(Int16.Parse(val))

				Case ClassDescriptor.FieldType.tpUShort
					Return New Key(UInt16.Parse(val))

				Case ClassDescriptor.FieldType.tpInt
					Return New Key(Int32.Parse(val))

				Case ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpEnum
					Return New Key(UInt32.Parse(val))

				Case ClassDescriptor.FieldType.tpOid
					Return New Key(ClassDescriptor.FieldType.tpOid, mapId(CInt(UInt32.Parse(val))))
				Case ClassDescriptor.FieldType.tpObject
					Return New Key(New PersistentStub(db, mapId(CInt(UInt32.Parse(val)))))

				Case ClassDescriptor.FieldType.tpLong
					Return New Key(Int64.Parse(val))

				Case ClassDescriptor.FieldType.tpULong
					Return New Key(UInt64.Parse(val))

				Case ClassDescriptor.FieldType.tpFloat
					Return New Key([Single].Parse(val))

				Case ClassDescriptor.FieldType.tpDouble
					Return New Key([Double].Parse(val))

				Case ClassDescriptor.FieldType.tpDecimal
					Return New Key([Decimal].Parse(val))

				Case ClassDescriptor.FieldType.tpGuid
					Return New Key(New Guid(val))

				Case ClassDescriptor.FieldType.tpString
					Return New Key(val)

				Case ClassDescriptor.FieldType.tpArrayOfByte
					If True Then
						Dim buf As Byte() = New Byte((val.Length >> 1) - 1) {}
						For i As Integer = 0 To buf.Length - 1
							buf(i) = CByte((getHexValue(val(i * 2)) << 4) Or getHexValue(val(i * 2 + 1)))
						Next
						Return New Key(buf)
					End If

				Case ClassDescriptor.FieldType.tpDate
					Return New Key(DateTime.Parse(val))
				Case Else

					throwException("Bad key type")
					Exit Select

			End Select
			Return Nothing
		End Function

		Friend Function parseInt(str As [String]) As Integer
			Return Int32.Parse(str)
		End Function

		Friend Function findClassByName(className As [String]) As Type
			Dim type As Type = DirectCast(classMap(className), Type)
			If type Is Nothing Then
				type = ClassDescriptor.lookup(db, className)
				classMap(className) = type
			End If
			Return type
		End Function

		Friend Sub createIndex(indexType As [String])
			Dim tkn As XMLScanner.Token
			Dim oid As Integer = 0
			Dim unique As Boolean = False
			Dim className As [String] = Nothing
			Dim fieldName As [String] = Nothing
			Dim fieldNames As [String]() = Nothing
			Dim autoinc As Long = 0
			Dim type As [String] = Nothing
			While (InlineAssignHelper(tkn, scanner.scan())) = XMLScanner.Token.IDENT
				Dim attrName As System.String = scanner.Identifier
				If scanner.scan() <> XMLScanner.Token.EQ OrElse scanner.scan() <> XMLScanner.Token.SCONST Then
					throwException("Attribute value expected")
				End If
				Dim attrValue As System.String = scanner.[String]
				If attrName.Equals("id") Then
					oid = mapId(parseInt(attrValue))
				ElseIf attrName.Equals("unique") Then
					unique = parseInt(attrValue) <> 0
				ElseIf attrName.Equals("class") Then
					className = attrValue
				ElseIf attrName.Equals("type") Then
					type = attrValue
				ElseIf attrName.Equals("autoinc") Then
					autoinc = parseInt(attrValue)
				ElseIf attrName.StartsWith("field") Then
					Dim len As Integer = attrName.Length
					If len = 5 Then
						fieldName = attrValue
					Else
						Dim fieldNo As Integer = Int32.Parse(attrName.Substring(5))
						If fieldNames Is Nothing OrElse fieldNames.Length <= fieldNo Then
							Dim newFieldNames As [String]() = New [String](fieldNo) {}
							If fieldNames IsNot Nothing Then
								Array.Copy(fieldNames, 0, newFieldNames, 0, fieldNames.Length)
							End If
							fieldNames = newFieldNames
						End If
						fieldNames(fieldNo) = attrValue
					End If
				End If
			End While
			If tkn <> XMLScanner.Token.GT Then
				throwException("Unclosed element tag")
			End If
			If oid = 0 Then
				throwException("ID is not specified or index")
			End If
			Dim desc As ClassDescriptor = db.getClassDescriptor(findClassByName(indexType))
			#If WITH_OLD_BTREE Then
			Dim btree As OldBtree = DirectCast(desc.newInstance(), OldBtree)
			If className IsNot Nothing Then
				Dim cls As Type = findClassByName(className)
				If fieldName IsNot Nothing Then
					btree.init(cls, ClassDescriptor.FieldType.tpLast, New String() {fieldName}, unique, autoinc)
				ElseIf fieldNames IsNot Nothing Then
					btree.init(cls, ClassDescriptor.FieldType.tpLast, fieldNames, unique, autoinc)
				Else
					throwException("Field name is not specified for field index")
				End If
			Else
				If type Is Nothing Then
					If indexType.StartsWith("Volante.Impl.PersistentSet") Then
					Else
						throwException("Key type is not specified for index")
					End If
				Else
					If indexType.StartsWith("Volante.impl.BitIndexImpl") Then
					Else
						btree.init(Nothing, mapType(type), Nothing, unique, autoinc)
					End If
				End If
			End If
			db.assignOid(btree, oid)
			#End If

			While (InlineAssignHelper(tkn, scanner.scan())) = XMLScanner.Token.LT
				If scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals("ref") Then
					throwException("<ref> element expected")
				End If
				#If WITH_OLD_BTREE Then
				Dim refElem As XMLElement = readElement("ref")
				Dim key As Key
				If fieldNames IsNot Nothing Then
					Dim values As [String]() = New [String](fieldNames.Length - 1) {}
					Dim types As ClassDescriptor.FieldType() = btree.FieldTypes
					For i As Integer = 0 To values.Length - 1
						values(i) = getAttribute(refElem, "key" & i)
					Next
					key = createCompoundKey(types, values)
				Else
					key = createKey(btree.FieldType, getAttribute(refElem, "key"))
				End If
				Dim obj As IPersistent = New PersistentStub(db, mapId(getIntAttribute(refElem, "id")))
					#End If
				btree.insert(key, obj, False)
			End While
			If tkn <> XMLScanner.Token.LTS OrElse scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals(indexType) OrElse scanner.scan() <> XMLScanner.Token.GT Then
				throwException("Element is not closed")
			End If
			#If WITH_OLD_BTREE Then
			Dim buf As New ByteBuffer()
			buf.extend(ObjectHeader.Sizeof)
			Dim size As Integer = db.packObject(btree, desc, ObjectHeader.Sizeof, buf, Nothing)
			Dim data As Byte() = buf.arr
			ObjectHeader.setSize(data, 0, size)
			ObjectHeader.setType(data, 0, desc.Oid)
			Dim pos As Long = db.allocate(size, 0)
			db.setPos(oid, pos Or DatabaseImpl.dbModifiedFlag)

			db.pool.put(pos And Not DatabaseImpl.dbFlagsMask, data, size)
			#End If
		End Sub

		Friend Sub createObject(elem As XMLElement)
			Dim desc As ClassDescriptor = db.getClassDescriptor(findClassByName(elem.Name))
			Dim oid As Integer = mapId(getIntAttribute(elem, "id"))
			Dim buf As New ByteBuffer()
			Dim offs As Integer = ObjectHeader.Sizeof
			buf.extend(offs)

			offs = packObject(elem, desc, offs, buf)

			ObjectHeader.setSize(buf.arr, 0, offs)
			ObjectHeader.setType(buf.arr, 0, desc.Oid)

			Dim pos As Long = db.allocate(offs, 0)
			db.setPos(oid, pos Or DatabaseImpl.dbModifiedFlag)
			db.pool.put(pos, buf.arr, offs)
		End Sub

		Friend Function getHexValue(ch As Char) As Integer
			If ch >= "0"C AndAlso ch <= "9"C Then
				Return ch - "0"C
			ElseIf ch >= "A"C AndAlso ch <= "F"C Then
				Return ch - "A"C + 10
			ElseIf ch >= "a"C AndAlso ch <= "f"C Then
				Return ch - "a"C + 10
			Else
				throwException("Bad hexadecimal constant")
			End If
			Return -1
		End Function

		Friend Function importBinary(elem As XMLElement, offs As Integer, buf As ByteBuffer, fieldName As [String]) As Integer
			If elem Is Nothing OrElse elem.isNullValue() Then
				buf.extend(offs + 4)
				Bytes.pack4(buf.arr, offs, -1)
				offs += 4
			ElseIf elem.isStringValue() Then
				Dim hexStr As [String] = elem.StringValue
				Dim len As Integer = hexStr.Length
				If hexStr.StartsWith("#") Then
					buf.extend(offs + 4 + len \ 2 - 1)
					Bytes.pack4(buf.arr, offs, -2 - getHexValue(hexStr(1)))
					offs += 4
					For j As Integer = 2 To len - 1 Step 2
						buf.arr(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte((getHexValue(hexStr(j)) << 4) Or getHexValue(hexStr(j + 1)))
					Next
				Else
					buf.extend(offs + 4 + len \ 2)
					Bytes.pack4(buf.arr, offs, len \ 2)
					offs += 4
					For j As Integer = 0 To len - 1 Step 2
						buf.arr(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte((getHexValue(hexStr(j)) << 4) Or getHexValue(hexStr(j + 1)))
					Next
				End If
			Else
				Dim refElem As XMLElement = elem.getSibling("ref")
				If refElem IsNot Nothing Then
					buf.extend(offs + 4)
					Bytes.pack4(buf.arr, offs, mapId(getIntAttribute(refElem, "id")))
					offs += 4
				Else
					Dim item As XMLElement = elem.getSibling("element")
					Dim len As Integer = If((item Is Nothing), 0, item.Counter)
					buf.extend(offs + 4 + len)
					Bytes.pack4(buf.arr, offs, len)
					offs += 4
					While System.Threading.Interlocked.Decrement(len) >= 0
						If item.isIntValue() Then
							buf.arr(offs) = CByte(item.IntValue)
						ElseIf item.isRealValue() Then
							buf.arr(offs) = CByte(Math.Truncate(item.RealValue))
						Else
							throwException("Conversion for field " & fieldName & " is not possible")
						End If
						item = item.NextSibling
						offs += 1
					End While
				End If
			End If
			Return offs
		End Function

		Friend Function packObject(objElem As XMLElement, desc As ClassDescriptor, offs As Integer, buf As ByteBuffer) As Integer
			Dim flds As ClassDescriptor.FieldDescriptor() = desc.allFields
			Dim i As Integer = 0, n As Integer = flds.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = flds(i)
				Dim f As FieldInfo = fd.field
				Dim fieldName As [String] = fd.fieldName
				Dim elem As XMLElement = If((objElem IsNot Nothing), objElem.getSibling(fieldName), Nothing)

				Select Case fd.type
					Case ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte
						buf.extend(offs + 1)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								buf.arr(offs) = CByte(elem.IntValue)
							ElseIf elem.isRealValue() Then
								buf.arr(offs) = CByte(Math.Truncate(elem.RealValue))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 1
						Continue Select

					Case ClassDescriptor.FieldType.tpBoolean
						buf.extend(offs + 1)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								buf.arr(offs) = CByte(If(elem.IntValue <> 0, 1, 0))
							ElseIf elem.isRealValue() Then
								buf.arr(offs) = CByte(If(elem.RealValue <> 0.0, 1, 0))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 1
						Continue Select

					Case ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort, ClassDescriptor.FieldType.tpChar
						buf.extend(offs + 2)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.pack2(buf.arr, offs, CShort(elem.IntValue))
							ElseIf elem.isRealValue() Then
								Bytes.pack2(buf.arr, offs, CShort(Math.Truncate(elem.RealValue)))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 2
						Continue Select

					Case ClassDescriptor.FieldType.tpEnum
						buf.extend(offs + 4)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.pack4(buf.arr, offs, CInt(elem.IntValue))
							ElseIf elem.isRealValue() Then
								Bytes.pack4(buf.arr, offs, CInt(Math.Truncate(elem.RealValue)))
							ElseIf elem.isStringValue() Then
								Try
									#If CF Then
									Bytes.pack4(buf.arr, offs, CInt(ClassDescriptor.parseEnum(f.FieldType, elem.StringValue)))
									#Else
										#End If
									Bytes.pack4(buf.arr, offs, CInt([Enum].Parse(f.FieldType, elem.StringValue)))
								Catch generatedExceptionName As ArgumentException
									throwException("Invalid enum value")
								End Try
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 4
						Continue Select

					Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt
						buf.extend(offs + 4)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.pack4(buf.arr, offs, CInt(elem.IntValue))
							ElseIf elem.isRealValue() Then
								Bytes.pack4(buf.arr, offs, CInt(Math.Truncate(elem.RealValue)))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 4
						Continue Select

					Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong
						buf.extend(offs + 8)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.pack8(buf.arr, offs, elem.IntValue)
							ElseIf elem.isRealValue() Then
								Bytes.pack8(buf.arr, offs, CLng(Math.Truncate(elem.RealValue)))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 8
						Continue Select

					Case ClassDescriptor.FieldType.tpFloat
						buf.extend(offs + 4)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.packF4(buf.arr, offs, CSng(elem.IntValue))
							ElseIf elem.isRealValue() Then
								Bytes.packF4(buf.arr, offs, CSng(elem.RealValue))
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 4
						Continue Select

					Case ClassDescriptor.FieldType.tpDouble
						buf.extend(offs + 8)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.packF8(buf.arr, offs, CDbl(elem.IntValue))
							ElseIf elem.isRealValue() Then
								Bytes.packF8(buf.arr, offs, elem.RealValue)
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 8
						Continue Select

					Case ClassDescriptor.FieldType.tpDecimal
						buf.extend(offs + 16)
						If elem IsNot Nothing Then
							Dim d As Decimal = 0
							If elem.isIntValue() Then
								d = elem.IntValue
							ElseIf elem.isRealValue() Then
								d = CDec(elem.RealValue)
							ElseIf elem.isStringValue() Then
								Try
									d = [Decimal].Parse(elem.StringValue)
								Catch generatedExceptionName As FormatException
									throwException("Invalid date")
								End Try
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If

							Bytes.packDecimal(buf.arr, offs, d)
						End If
						offs += 16
						Continue Select

					Case ClassDescriptor.FieldType.tpGuid
						buf.extend(offs + 16)
						If elem IsNot Nothing Then
							If elem.isStringValue() Then
								Dim guid As New Guid(elem.StringValue)
								Dim bits As Byte() = guid.ToByteArray()
								Array.Copy(bits, 0, buf.arr, offs, 16)
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 16
						Continue Select

					Case ClassDescriptor.FieldType.tpDate
						buf.extend(offs + 8)
						If elem IsNot Nothing Then
							If elem.isIntValue() Then
								Bytes.pack8(buf.arr, offs, elem.IntValue)
							ElseIf elem.isNullValue() Then
								Bytes.pack8(buf.arr, offs, -1)
							ElseIf elem.isStringValue() Then
								Try
									Bytes.packDate(buf.arr, offs, DateTime.Parse(elem.StringValue))
								Catch generatedExceptionName As FormatException
									throwException("Invalid date")
								End Try
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
						End If
						offs += 8
						Continue Select

					Case ClassDescriptor.FieldType.tpString
						If elem IsNot Nothing Then
							Dim val As System.String = Nothing
							If elem.isIntValue() Then
								val = System.Convert.ToString(elem.IntValue)
							ElseIf elem.isRealValue() Then
								val = elem.RealValue.ToString()
							ElseIf elem.isStringValue() Then
								val = elem.StringValue
							ElseIf elem.isNullValue() Then
								val = Nothing
							Else
								throwException("Conversion for field " & fieldName & " is not possible")
							End If
							offs = buf.packString(offs, val)
							Continue Select
						End If
						buf.extend(offs + 4)
						Bytes.pack4(buf.arr, offs, -1)
						offs += 4
						Continue Select

					Case ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpObject
						If True Then
							Dim oid As Integer = 0
							If elem IsNot Nothing Then
								Dim refElem As XMLElement = elem.getSibling("ref")
								If refElem Is Nothing Then
									throwException("<ref> element expected")
								End If
								oid = mapId(getIntAttribute(refElem, "id"))
							End If
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, oid)
							offs += 4
							Continue Select
						End If

					Case ClassDescriptor.FieldType.tpValue
						offs = packObject(elem, fd.valueDesc, offs, buf)
						Continue Select

					Case ClassDescriptor.FieldType.tpRaw, ClassDescriptor.FieldType.tpArrayOfByte, ClassDescriptor.FieldType.tpArrayOfSByte
						offs = importBinary(elem, offs, buf, fieldName)
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfBoolean
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									buf.arr(offs) = CByte(If(item.IntValue <> 0, 1, 0))
								ElseIf item.isRealValue() Then
									buf.arr(offs) = CByte(If(item.RealValue <> 0.0, 1, 0))
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 1
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfChar, ClassDescriptor.FieldType.tpArrayOfShort, ClassDescriptor.FieldType.tpArrayOfUShort
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 2)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.pack2(buf.arr, offs, CShort(item.IntValue))
								ElseIf item.isRealValue() Then
									Bytes.pack2(buf.arr, offs, CShort(Math.Truncate(item.RealValue)))
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 2
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfEnum
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							Dim elemType As Type = f.FieldType.GetElementType()
							buf.extend(offs + 4 + len * 4)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.pack4(buf.arr, offs, CInt(item.IntValue))
								ElseIf item.isRealValue() Then
									Bytes.pack4(buf.arr, offs, CInt(Math.Truncate(item.RealValue)))
								ElseIf item.isStringValue() Then
									Try
										#If CF Then
										Bytes.pack4(buf.arr, offs, CInt(ClassDescriptor.parseEnum(elemType, item.StringValue)))
										#Else
											#End If
										Bytes.pack4(buf.arr, offs, CInt([Enum].Parse(elemType, item.StringValue)))
									Catch generatedExceptionName As ArgumentException
										throwException("Invalid enum value")
									End Try
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 4
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfInt, ClassDescriptor.FieldType.tpArrayOfUInt
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 4)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.pack4(buf.arr, offs, CInt(item.IntValue))
								ElseIf item.isRealValue() Then
									Bytes.pack4(buf.arr, offs, CInt(Math.Truncate(item.RealValue)))
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 4
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfLong, ClassDescriptor.FieldType.tpArrayOfULong
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 8)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.pack8(buf.arr, offs, item.IntValue)
								ElseIf item.isRealValue() Then
									Bytes.pack8(buf.arr, offs, CLng(Math.Truncate(item.RealValue)))
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 8
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfFloat
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 4)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.packF4(buf.arr, offs, CSng(item.IntValue))
								ElseIf item.isRealValue() Then
									Bytes.packF4(buf.arr, offs, CSng(item.RealValue))
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 4
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfDouble
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 8)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isIntValue() Then
									Bytes.packF8(buf.arr, offs, CDbl(item.IntValue))
								ElseIf item.isRealValue() Then
									Bytes.packF8(buf.arr, offs, item.RealValue)
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								item = item.NextSibling
								offs += 8
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfDate
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 8)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isNullValue() Then
									Bytes.pack8(buf.arr, offs, -1)
								ElseIf item.isStringValue() Then
									Try
										Bytes.packDate(buf.arr, offs, DateTime.Parse(item.StringValue))
									Catch generatedExceptionName As FormatException
										throwException("Conversion for field " & fieldName & " is not possible")
									End Try
								End If
								item = item.NextSibling
								offs += 8
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfDecimal
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 16)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isStringValue() Then
									Try
										Bytes.packDecimal(buf.arr, offs, [Decimal].Parse(item.StringValue))
									Catch generatedExceptionName As FormatException
										throwException("Conversion for field " & fieldName & " is not possible")
									End Try
								End If
								item = item.NextSibling
								offs += 16
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfGuid
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 16)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								If item.isStringValue() Then
									Try
										Bytes.packGuid(buf.arr, offs, New Guid(item.StringValue))
									Catch generatedExceptionName As FormatException
										throwException("Conversion for field " & fieldName & " is not possible")
									End Try
								End If
								item = item.NextSibling
								offs += 16
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfString
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								Dim val As System.String = Nothing
								If item.isIntValue() Then
									val = System.Convert.ToString(item.IntValue)
								ElseIf item.isRealValue() Then
									val = item.RealValue.ToString()
								ElseIf item.isStringValue() Then
									val = item.StringValue
								ElseIf item.isNullValue() Then
									val = Nothing
								Else
									throwException("Conversion for field " & fieldName & " is not possible")
								End If
								offs = buf.packString(offs, val)
								item = item.NextSibling
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfObject, ClassDescriptor.FieldType.tpArrayOfOid, ClassDescriptor.FieldType.tpLink
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							buf.extend(offs + 4 + len * 4)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								Dim href As XMLElement = item.getSibling("ref")
								If href Is Nothing Then
									throwException("<ref> element expected")
								End If
								Dim oid As Integer = mapId(getIntAttribute(href, "id"))
								Bytes.pack4(buf.arr, offs, oid)
								item = item.NextSibling
								offs += 4
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfValue
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							Dim elemDesc As ClassDescriptor = fd.valueDesc
							While System.Threading.Interlocked.Decrement(len) >= 0
								offs = packObject(item, elemDesc, offs, buf)
								item = item.NextSibling
							End While
						End If
						Continue Select

					Case ClassDescriptor.FieldType.tpArrayOfRaw
						If elem Is Nothing OrElse elem.isNullValue() Then
							buf.extend(offs + 4)
							Bytes.pack4(buf.arr, offs, -1)
							offs += 4
						Else
							Dim item As XMLElement = elem.getSibling("element")
							Dim len As Integer = If((item Is Nothing), 0, item.Counter)
							Bytes.pack4(buf.arr, offs, len)
							offs += 4
							While System.Threading.Interlocked.Decrement(len) >= 0
								offs = importBinary(item, offs, buf, fieldName)
								item = item.NextSibling
							End While
						End If
						Continue Select
				End Select
				i += 1
			End While
			Return offs
		End Function

		Friend Function readElement(name As System.String) As XMLElement
			Dim elem As New XMLElement(name)
			Dim attribute As System.String
			Dim tkn As XMLScanner.Token
			While True
				Select Case scanner.scan()
					Case XMLScanner.Token.GTS
						Return elem

					Case XMLScanner.Token.GT
						While (InlineAssignHelper(tkn, scanner.scan())) = XMLScanner.Token.LT
							If scanner.scan() <> XMLScanner.Token.IDENT Then
								throwException("Element name expected")
							End If
							Dim siblingName As System.String = scanner.Identifier
							Dim sibling As XMLElement = readElement(siblingName)
							elem.addSibling(sibling)
						End While
						Select Case tkn
							Case XMLScanner.Token.SCONST
								elem.StringValue = scanner.[String]
								tkn = scanner.scan()
								Exit Select

							Case XMLScanner.Token.ICONST
								elem.IntValue = scanner.Int
								tkn = scanner.scan()
								Exit Select

							Case XMLScanner.Token.FCONST
								elem.RealValue = scanner.Real
								tkn = scanner.scan()
								Exit Select

							Case XMLScanner.Token.IDENT
								If scanner.Identifier.Equals("null") Then
									elem.setNullValue()
								Else
									elem.StringValue = scanner.Identifier
								End If
								tkn = scanner.scan()
								Exit Select

						End Select
						If tkn <> XMLScanner.Token.LTS OrElse scanner.scan() <> XMLScanner.Token.IDENT OrElse Not scanner.Identifier.Equals(name) OrElse scanner.scan() <> XMLScanner.Token.GT Then
							throwException("Element is not closed")
						End If
						Return elem

					Case XMLScanner.Token.IDENT
						attribute = scanner.Identifier
						If scanner.scan() <> XMLScanner.Token.EQ OrElse scanner.scan() <> XMLScanner.Token.SCONST Then
							throwException("Attribute value expected")
						End If
						elem.addAttribute(attribute, scanner.[String])
						Continue Select
					Case Else

						throwException("Unexpected token")
						Exit Select

				End Select
			End While
		End Function

		Friend Sub throwException(message As System.String)
			Throw New XmlImportException(scanner.Line, scanner.Column, message)
		End Sub

		Friend db As DatabaseImpl
		Friend scanner As XMLScanner
		Friend classMap As Hashtable
		Friend idMap As Integer()

		Friend Class XMLScanner
			Friend Overridable ReadOnly Property Identifier() As System.String
				Get
					Return ident
				End Get
			End Property

			Friend Overridable ReadOnly Property [String]() As System.String
				Get
					Return New [String](sconst, 0, slen)
				End Get
			End Property

			Friend Overridable ReadOnly Property Int() As Long
				Get
					Return iconst
				End Get
			End Property

			Friend Overridable ReadOnly Property Real() As Double
				Get
					Return fconst
				End Get
			End Property

			Friend Overridable ReadOnly Property Line() As Integer
				Get
					Return m_line
				End Get
			End Property

			Friend Overridable ReadOnly Property Column() As Integer
				Get
					Return m_column
				End Get
			End Property

			Friend Enum Token
				IDENT
				SCONST
				ICONST
				FCONST
				LT
				GT
				LTS
				GTS
				EQ
				EOF
			End Enum

			Friend reader As System.IO.StreamReader
			Friend m_line As Integer
			Friend m_column As Integer
			Friend sconst As Char()
			Friend iconst As Long
			Friend fconst As Double
			Friend slen As Integer
			Friend ident As [String]
			Friend size As Integer
			Friend ungetChar As Integer
			Friend hasUngetChar As Boolean

			Friend Sub New(reader As System.IO.StreamReader)
				Me.reader = reader
				sconst = New Char(InlineAssignHelper(size, 1024) - 1) {}
				m_line = 1
				m_column = 0
				hasUngetChar = False
			End Sub

			Friend Function [get]() As Integer
				If hasUngetChar Then
					hasUngetChar = False
					Return ungetChar
				End If
				Dim ch As Integer = reader.Read()
				If ch = ControlChars.Lf Then
					m_line += 1
					m_column = 0
				ElseIf ch = ControlChars.Tab Then
					m_column += (m_column + 8) And Not 7
				Else
					m_column += 1
				End If
				Return ch
			End Function

			Friend Sub unget(ch As Integer)
				If ch = ControlChars.Lf Then
					m_line -= 1
				Else
					m_column -= 1
				End If
				ungetChar = ch
				hasUngetChar = True
			End Sub

			Friend Function scan() As Token
				Dim i As Integer, ch As Integer
				Dim floatingPoint As Boolean

				While True
					Do
						If (InlineAssignHelper(ch, [get]())) < 0 Then
							Return Token.EOF
						End If
					Loop While ch <= " "C

					Select Case ch
						Case "<"C
							ch = [get]()
							If ch = "?"C Then
								While (InlineAssignHelper(ch, [get]())) <> "?"C
									If ch < 0 Then
										Throw New XmlImportException(m_line, m_column, "Bad XML file format")
									End If
								End While
								If (InlineAssignHelper(ch, [get]())) <> ">"C Then
									Throw New XmlImportException(m_line, m_column, "Bad XML file format")
								End If
								Continue Select
							End If
							If ch <> "/"C Then
								unget(ch)
								Return Token.LT
							End If
							Return Token.LTS

						Case ">"C
							Return Token.GT

						Case "/"C
							ch = [get]()
							If ch <> ">"C Then
								unget(ch)
								Throw New XmlImportException(m_line, m_column, "Bad XML file format")
							End If
							Return Token.GTS

						Case "="C
							Return Token.EQ

						Case """"C
							i = 0
							While True
								ch = [get]()
								If ch < 0 Then
									Throw New XmlImportException(m_line, m_column, "Bad XML file format")
								ElseIf ch = "&"C Then
									Select Case [get]()
										Case "a"C
											If [get]() <> "m"C OrElse [get]() <> "p"C OrElse [get]() <> ";"C Then
												Throw New XmlImportException(m_line, m_column, "Bad XML file format")
											End If
											ch = "&"C
											Exit Select

										Case "l"C
											If [get]() <> "t"C OrElse [get]() <> ";"C Then
												Throw New XmlImportException(m_line, m_column, "Bad XML file format")
											End If
											ch = "<"C
											Exit Select

										Case "g"C
											If [get]() <> "t"C OrElse [get]() <> ";"C Then
												Throw New XmlImportException(m_line, m_column, "Bad XML file format")
											End If
											ch = ">"C
											Exit Select

										Case "q"C
											If [get]() <> "u"C OrElse [get]() <> "o"C OrElse [get]() <> "t"C OrElse [get]() <> ";"C Then
												Throw New XmlImportException(m_line, m_column, "Bad XML file format")
											End If
											ch = """"C
											Exit Select
										Case Else

											Throw New XmlImportException(m_line, m_column, "Bad XML file format")

									End Select
								ElseIf ch = """"C Then
									slen = i
									Return Token.SCONST
								End If
								If i = size Then
									Dim newBuf As Char() = New Char(size *= 2 - 1) {}
									Array.Copy(sconst, 0, newBuf, 0, i)
									sconst = newBuf
								End If
								sconst(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1)) = ChrW(ch)
							End While

						Case "-"C, "0"C, "1"C, "2"C, "3"C, "4"C, _
							"5"C, "6"C, "7"C, "8"C, "9"C
							i = 0
							floatingPoint = False
							While True
								If Not System.[Char].IsDigit(ChrW(ch)) AndAlso ch <> "-"C AndAlso ch <> "+"C AndAlso ch <> "."C AndAlso ch <> "E"C Then
									unget(ch)
									Try
										If floatingPoint Then
											fconst = System.[Double].Parse(New [String](sconst, 0, i))
											Return Token.FCONST
										Else
											iconst = If(sconst(0) = "-"C, System.Int64.Parse(New [String](sconst, 0, i)), CLng(System.UInt64.Parse(New [String](sconst, 0, i))))
											Return Token.ICONST
										End If
									Catch generatedExceptionName As System.FormatException
										Throw New XmlImportException(m_line, m_column, "Bad XML file format")
									End Try
								End If
								If i = size Then
									Throw New XmlImportException(m_line, m_column, "Bad XML file format")
								End If
								sconst(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1)) = ChrW(ch)
								If ch = "."C Then
									floatingPoint = True
								End If
								ch = [get]()
							End While
						Case Else

							i = 0
							While System.[Char].IsLetterOrDigit(ChrW(ch)) OrElse ch = "-"C OrElse ch = ":"C OrElse ch = "_"C OrElse ch = "."C
								If i = size Then
									Throw New XmlImportException(m_line, m_column, "Bad XML file format")
								End If
								If ch = "-"C Then
									ch = "+"C
								End If
								sconst(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1)) = ChrW(ch)
								ch = [get]()
							End While
							unget(ch)
							If i = 0 Then
								Throw New XmlImportException(m_line, m_column, "Bad XML file format")
							End If
							ident = New [String](sconst, 0, i)
							ident = ident.Replace(".1", "`")
							ident = ident.Replace(".2", ",")
							ident = ident.Replace(".3", "[")
							ident = ident.Replace(".4", "]")
							ident = ident.Replace(".5", "=")
							Return Token.IDENT
					End Select
				End While
			End Function
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
#End If

