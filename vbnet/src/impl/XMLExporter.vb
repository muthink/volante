#If WITH_XML Then
Imports System.Reflection
Imports System.Diagnostics
Imports System.Text
Imports Volante
Namespace Volante.Impl

	Public Class XmlExporter
		Public Sub New(db As DatabaseImpl, writer As System.IO.StreamWriter)
			Me.db = db
			Me.writer = writer
		End Sub

		Public Overridable Sub exportDatabase(rootOid As Integer)
			writer.Write("<?xml version=""1.0"" encoding=""UTF-8""?>" & vbLf)
			writer.Write("<database root=""" & rootOid & """>" & vbLf)
			exportedBitmap = New Integer((db.currIndexSize + 31) / 32 - 1) {}
			markedBitmap = New Integer((db.currIndexSize + 31) / 32 - 1) {}
			markedBitmap(rootOid >> 5) = markedBitmap(rootOid >> 5) Or 1 << (rootOid And 31)
			Dim nExportedObjects As Integer
			Do
				nExportedObjects = 0
				For i As Integer = 0 To markedBitmap.Length - 1
					Dim mask As Integer = markedBitmap(i)
					If mask <> 0 Then
						Dim j As Integer = 0, bit As Integer = 1
						While j < 32
							If (mask And bit) <> 0 Then
								Dim oid As Integer = (i << 5) + j
								exportedBitmap(i) = exportedBitmap(i) Or bit
								markedBitmap(i) = markedBitmap(i) And Not bit
								Dim obj As Byte() = db.[get](oid)
								Dim typeOid As Integer = ObjectHeader.[getType](obj, 0)
								Dim desc As ClassDescriptor = db.findClassDescriptor(typeOid)
								Dim name As String = desc.name
								#If WITH_OLD_BTREE Then
								If GetType(OldBtree).IsAssignableFrom(desc.cls) Then
									Dim t As Type = desc.cls.GetGenericTypeDefinition()
									If t Is GetType(OldBtree(Of , )) OrElse t Is GetType(IBitIndex(Of )) Then
										exportIndex(oid, obj, name)
									ElseIf t Is GetType(OldPersistentSet(Of )) Then
										exportSet(oid, obj, name)
									ElseIf t Is GetType(OldBtreeFieldIndex(Of , )) Then
										exportFieldIndex(oid, obj, name)
									ElseIf t Is GetType(OldBtreeMultiFieldIndex(Of )) Then
										exportMultiFieldIndex(oid, obj, name)
									End If
								Else
									#End If
									Dim className As [String] = exportIdentifier(desc.name)
									writer.Write(" <" & className & " id=""" & oid & """>" & vbLf)
									exportObject(desc, obj, ObjectHeader.Sizeof, 2)
									writer.Write(" </" & className & ">" & vbLf)
								End If
								nExportedObjects += 1
							End If
							j += 1
							bit <<= 1
						End While
					End If
				Next
			Loop While nExportedObjects <> 0
			writer.Write("</database>" & vbLf)
		End Sub

		Friend Function exportIdentifier(name As [String]) As [String]
			name = name.Replace("+"C, "-"C)
			name = name.Replace("`", ".1")
			name = name.Replace(",", ".2")
			name = name.Replace("[", ".3")
			name = name.Replace("]", ".4")
			name = name.Replace("=", ".5")
			Return name
		End Function

		#If WITH_OLD_BTREE Then
		Private Function createBtree(oid As Integer, data As Byte()) As OldBtree
			Dim btree As OldBtree = db.createBtreeStub(data, 0)
			db.assignOid(btree, oid)
			Return btree
		End Function

		Friend Sub exportSet(oid As Integer, data As Byte(), name As String)
			Dim btree As OldBtree = createBtree(oid, data)
			name = exportIdentifier(name)
			writer.Write(" <" & name & " id=""" & oid & """>" & vbLf)
			btree.export(Me)
			writer.Write(" </" & name & ">" & vbLf)
		End Sub

		Friend Sub exportIndex(oid As Integer, data As Byte(), name As String)
			Dim btree As OldBtree = createBtree(oid, data)
			name = exportIdentifier(name)
			writer.Write((" <" & name & " id=""" & oid & """ unique=""" & (If(btree.IsUnique, "1"C, "0"C)) & """ type=""") + btree.FieldType & """>" & vbLf)
			btree.export(Me)
			writer.Write(" </" & name & ">" & vbLf)
		End Sub

		Friend Sub exportFieldIndex(oid As Integer, data As Byte(), name As String)
			Dim btree As OldBtree = createBtree(oid, data)
			name = exportIdentifier(name)
			writer.Write(" <" & name & " id=""" & oid & """ unique=""" & (If(btree.IsUnique, "1"C, "0"C)) & """ class=")
			Dim offs As Integer = exportString(data, btree.HeaderSize)
			writer.Write(" field=")
			offs = exportString(data, offs)
			writer.Write(" autoinc=""" & Bytes.unpack8(data, offs) & """>" & vbLf)
			btree.export(Me)
			writer.Write(" </" & name & ">" & vbLf)
		End Sub

		Friend Sub exportMultiFieldIndex(oid As Integer, data As Byte(), name As String)
			Dim btree As OldBtree = createBtree(oid, data)
			name = exportIdentifier(name)
			writer.Write(" <" & name & " id=""" & oid & """ unique=""" & (If(btree.IsUnique, "1"C, "0"C)) & """ class=")
			Dim offs As Integer = exportString(data, btree.HeaderSize)
			Dim nFields As Integer = Bytes.unpack4(data, offs)
			offs += 4
			For i As Integer = 0 To nFields - 1
				writer.Write(" field" & i & "=")
				offs = exportString(data, offs)
			Next
			writer.Write(">" & vbLf)
			Dim nTypes As Integer = Bytes.unpack4(data, offs)
			offs += 4
			compoundKeyTypes = New ClassDescriptor.FieldType(nTypes - 1) {}
			For i As Integer = 0 To nTypes - 1
				compoundKeyTypes(i) = DirectCast(Bytes.unpack4(data, offs), ClassDescriptor.FieldType)
				offs += 4
			Next
			btree.export(Me)
			compoundKeyTypes = Nothing
			writer.Write(" </" & name & ">" & vbLf)
		End Sub
		#End If

		Private Function exportKey(body As Byte(), offs As Integer, size As Integer, type As ClassDescriptor.FieldType) As Integer
			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					writer.Write(If(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0, "1", "0"))
					Exit Select

				Case ClassDescriptor.FieldType.tpByte
					writer.Write(System.Convert.ToString(CByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))))
					Exit Select

				Case ClassDescriptor.FieldType.tpSByte
					writer.Write(System.Convert.ToString(CSByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))))
					Exit Select

				Case ClassDescriptor.FieldType.tpChar
					writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpShort
					writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpUShort
					writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
					offs += 2
					Exit Select

				Case ClassDescriptor.FieldType.tpInt
					writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)))
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpEnum
					writer.Write(System.Convert.ToString(CUInt(Bytes.unpack4(body, offs))))
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpLong
					writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)))
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpULong
					writer.Write(System.Convert.ToString(CULng(Bytes.unpack8(body, offs))))
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpFloat
					writer.Write(System.Convert.ToString(Bytes.unpackF4(body, offs)))
					offs += 4
					Exit Select

				Case ClassDescriptor.FieldType.tpDouble
					writer.Write(System.Convert.ToString(Bytes.unpackF8(body, offs)))
					offs += 8
					Exit Select

				Case ClassDescriptor.FieldType.tpGuid
					writer.Write(Bytes.unpackGuid(body, offs).ToString())
					offs += 16
					Exit Select

				Case ClassDescriptor.FieldType.tpDecimal
					writer.Write(Bytes.unpackDecimal(body, offs).ToString())
					offs += 16
					Exit Select

				Case ClassDescriptor.FieldType.tpString

					If size < 0 Then
						Dim s As String
						offs = Bytes.unpackString(body, offs - 4, s)
						For i As Integer = 0 To s.Length - 1
							exportChar(s(i))
						Next
					Else
						For i As Integer = 0 To size - 1
							exportChar(CChar(Bytes.unpack2(body, offs)))
							offs += 2
						Next
					End If
					Exit Select

				Case ClassDescriptor.FieldType.tpArrayOfByte
					For i As Integer = 0 To size - 1
						Dim b As Byte = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
						writer.Write(hexDigit((b >> 4) And &Hf))
						writer.Write(hexDigit(b And &Hf))
					Next
					Exit Select

				Case ClassDescriptor.FieldType.tpDate
					writer.Write(Bytes.unpackDate(body, offs).ToString())
					offs += 8
					Exit Select
				Case Else

					Debug.Assert(False, "Invalid type")
					Exit Select
			End Select
			Return offs
		End Function

		Private Sub exportCompoundKey(body As Byte(), offs As Integer, size As Integer, type As ClassDescriptor.FieldType)
			Debug.Assert(type = ClassDescriptor.FieldType.tpArrayOfByte)
			Dim [end] As Integer = offs + size
			For i As Integer = 0 To compoundKeyTypes.Length - 1
				type = compoundKeyTypes(i)
				If type = ClassDescriptor.FieldType.tpArrayOfByte OrElse type = ClassDescriptor.FieldType.tpString Then
					size = Bytes.unpack4(body, offs)
					offs += 4
				End If
				writer.Write(" key" & i & "=""")
				offs = exportKey(body, offs, size, type)
				writer.Write("""")
			Next
			Debug.Assert(offs = [end])
		End Sub

		Friend Sub exportAssoc(oid As Integer, body As Byte(), offs As Integer, size As Integer, type As ClassDescriptor.FieldType)
			writer.Write("  <ref id=""" & oid & """")
			If (exportedBitmap(oid >> 5) And (1 << (oid And 31))) = 0 Then
				markedBitmap(oid >> 5) = markedBitmap(oid >> 5) Or 1 << (oid And 31)
			End If
			If compoundKeyTypes IsNot Nothing Then
				exportCompoundKey(body, offs, size, type)
			Else
				writer.Write(" key=""")
				exportKey(body, offs, size, type)
				writer.Write("""")
			End If
			writer.Write("/>" & vbLf)
		End Sub

		Friend Sub indentation(indent As Integer)
			While System.Threading.Interlocked.Decrement(indent) >= 0
				writer.Write(" "C)
			End While
		End Sub

		Friend Sub exportChar(ch As Char)
			Select Case ch
				Case "<"C
					writer.Write("&lt;")
					Exit Select

				Case ">"C
					writer.Write("&gt;")
					Exit Select

				Case "&"C
					writer.Write("&amp;")
					Exit Select

				Case """"C
					writer.Write("&quot;")
					Exit Select
				Case Else

					writer.Write(ch)
					Exit Select

			End Select
		End Sub

		Friend Function exportString(body As Byte(), offs As Integer) As Integer
			Dim len As Integer = Bytes.unpack4(body, offs)
			offs += 4
			If len >= 0 Then
				Debug.Assert(False)
			ElseIf len < -1 Then
				writer.Write("""")
				Dim s As String = Encoding.UTF8.GetString(body, offs, -len - 2)
				offs -= len + 2
				Dim i As Integer = 0, n As Integer = s.Length
				While i < n
					exportChar(s(i))
					i += 1
				End While
				writer.Write("""")
			Else
				writer.Write("null")
			End If
			Return offs
		End Function

		Friend Shared hexDigit As Char() = New Char() {"0"C, "1"C, "2"C, "3"C, "4"C, "5"C, _
			"6"C, "7"C, "8"C, "9"C, "A"C, "B"C, _
			"C"C, "D"C, "E"C, "F"C}

		Friend Sub exportRef(oid As Integer)
			writer.Write("<ref id=""" & oid & """/>")
			If oid <> 0 AndAlso (exportedBitmap(oid >> 5) And (1 << (oid And 31))) = 0 Then
				markedBitmap(oid >> 5) = markedBitmap(oid >> 5) Or 1 << (oid And 31)
			End If
		End Sub

		Friend Function exportBinary(body As Byte(), offs As Integer) As Integer
			Dim len As Integer = Bytes.unpack4(body, offs)
			offs += 4
			If len < 0 Then
				If len = -2 - CInt(ClassDescriptor.FieldType.tpObject) Then
					exportRef(Bytes.unpack4(body, offs))
					offs += 4
				ElseIf len < -1 Then
					writer.Write("""#")
					writer.Write(hexDigit(-2 - len))
					len = ClassDescriptor.Sizeof(-2 - len)
					While System.Threading.Interlocked.Decrement(len) >= 0
						Dim b As Byte = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
						writer.Write(hexDigit((b >> 4) And &Hf))
						writer.Write(hexDigit(b And &Hf))
					End While
					writer.Write(""""C)
				Else
					writer.Write("null")
				End If
			Else
				writer.Write(""""C)
				While System.Threading.Interlocked.Decrement(len) >= 0
					Dim b As Byte = body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1))
					writer.Write(hexDigit((b >> 4) And &Hf))
					writer.Write(hexDigit(b And &Hf))
				End While
				writer.Write(""""C)
			End If
			Return offs
		End Function

		Friend Function exportObject(desc As ClassDescriptor, body As Byte(), offs As Integer, indent As Integer) As Integer
			Dim all As ClassDescriptor.FieldDescriptor() = desc.allFields

			Dim i As Integer = 0, n As Integer = all.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = all(i)
				Dim f As FieldInfo = fd.field
				indentation(indent)
				Dim fieldName As [String] = exportIdentifier(fd.fieldName)
				writer.Write("<" & fieldName & ">")
				Select Case fd.type
					Case ClassDescriptor.FieldType.tpBoolean
						writer.Write(If(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0, "1", "0"))
						Exit Select

					Case ClassDescriptor.FieldType.tpByte
						writer.Write(System.Convert.ToString(CByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))))
						Exit Select

					Case ClassDescriptor.FieldType.tpSByte
						writer.Write(System.Convert.ToString(CSByte(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)))))
						Exit Select

					Case ClassDescriptor.FieldType.tpChar
						writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpShort
						writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpUShort
						writer.Write(System.Convert.ToString(CUShort(Bytes.unpack2(body, offs))))
						offs += 2
						Exit Select

					Case ClassDescriptor.FieldType.tpInt
						writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpEnum
						writer.Write([Enum].ToObject(f.FieldType, Bytes.unpack4(body, offs)))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpUInt
						writer.Write(System.Convert.ToString(CUInt(Bytes.unpack4(body, offs))))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpLong
						writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)))
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpULong
						writer.Write(System.Convert.ToString(CULng(Bytes.unpack8(body, offs))))
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpFloat
						writer.Write(System.Convert.ToString(Bytes.unpackF4(body, offs)))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpDouble
						writer.Write(System.Convert.ToString(Bytes.unpackF8(body, offs)))
						offs += 8
						Exit Select

					Case ClassDescriptor.FieldType.tpGuid
						writer.Write("""" & Bytes.unpackGuid(body, offs) & """")
						offs += 16
						Exit Select

					Case ClassDescriptor.FieldType.tpDecimal
						writer.Write("""" & Bytes.unpackDecimal(body, offs) & """")
						offs += 16
						Exit Select

					Case ClassDescriptor.FieldType.tpString
						offs = exportString(body, offs)
						Exit Select

					Case ClassDescriptor.FieldType.tpDate
						If True Then
							Dim msec As Long = Bytes.unpack8(body, offs)
							offs += 8
							If msec >= 0 Then
								writer.Write("""" & New System.DateTime(msec) & """")
							Else
								writer.Write("null")
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
						exportRef(Bytes.unpack4(body, offs))
						offs += 4
						Exit Select

					Case ClassDescriptor.FieldType.tpValue
						writer.Write(ControlChars.Lf)
						offs = exportObject(fd.valueDesc, body, offs, indent + 1)
						indentation(indent)
						Exit Select

					Case ClassDescriptor.FieldType.tpRaw, ClassDescriptor.FieldType.tpArrayOfByte, ClassDescriptor.FieldType.tpArrayOfSByte
						offs = exportBinary(body, offs)
						Exit Select

					Case ClassDescriptor.FieldType.tpArrayOfBoolean
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & (If(body(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) <> 0, "1", "0")) & "</element>" & vbLf)
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfChar
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & (Bytes.unpack2(body, offs) And &Hffff) & "</element>" & vbLf)
									offs += 2
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfShort
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Bytes.unpack2(body, offs) & "</element>" & vbLf)
									offs += 2
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfUShort
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & CUShort(Bytes.unpack2(body, offs)) & "</element>" & vbLf)
									offs += 2
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfInt
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Bytes.unpack4(body, offs) & "</element>" & vbLf)
									offs += 4
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfEnum
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								Dim elemType As Type = f.FieldType.GetElementType()
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Convert.ToString([Enum].ToObject(elemType, Bytes.unpack4(body, offs))) & "</element>" & vbLf)
									offs += 4
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfUInt
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & CUInt(Bytes.unpack4(body, offs)) & "</element>" & vbLf)
									offs += 4
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfLong
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Bytes.unpack8(body, offs) & "</element>" & vbLf)
									offs += 8
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfULong
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & CULng(Bytes.unpack8(body, offs)) & "</element>" & vbLf)
									offs += 8
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfFloat
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Bytes.unpackF4(body, offs) & "</element>" & vbLf)
									offs += 4
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfDouble
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & Bytes.unpackF8(body, offs) & "</element>" & vbLf)
									offs += 8
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfDate
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>""" & Bytes.unpackDate(body, offs) & """</element>" & vbLf)
									offs += 8
								End While
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfGuid
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									writer.Write("<element>""" & Bytes.unpackGuid(body, offs) & """</element>" & vbLf)
									offs += 16
								End While
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfDecimal
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									writer.Write("<element>""" & Bytes.unpackDecimal(body, offs) & """</element>" & vbLf)
									offs += 16
								End While
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfString
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>")
									offs = exportString(body, offs)
									writer.Write("</element>" & vbLf)
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpLink, ClassDescriptor.FieldType.tpArrayOfObject, ClassDescriptor.FieldType.tpArrayOfOid
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									Dim oid As Integer = Bytes.unpack4(body, offs)
									If oid <> 0 AndAlso (exportedBitmap(oid >> 5) And (1 << (oid And 31))) = 0 Then
										markedBitmap(oid >> 5) = markedBitmap(oid >> 5) Or 1 << (oid And 31)
									End If
									writer.Write("<element><ref id=""" & oid & """/></element>" & vbLf)
									offs += 4
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfValue
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>" & vbLf)
									offs = exportObject(fd.valueDesc, body, offs, indent + 2)
									indentation(indent + 1)
									writer.Write("</element>" & vbLf)
								End While
								indentation(indent)
							End If
							Exit Select
						End If

					Case ClassDescriptor.FieldType.tpArrayOfRaw
						If True Then
							Dim len As Integer = Bytes.unpack4(body, offs)
							offs += 4
							If len < 0 Then
								writer.Write("null")
							Else
								writer.Write(ControlChars.Lf)
								While System.Threading.Interlocked.Decrement(len) >= 0
									indentation(indent + 1)
									writer.Write("<element>")
									offs = exportBinary(body, offs)
									writer.Write("</element>" & vbLf)
								End While
								indentation(indent)
							End If
							Exit Select
						End If
				End Select
				writer.Write("</" & fieldName & ">" & vbLf)
				i += 1
			End While
			Return offs
		End Function

		Private db As DatabaseImpl
		Private writer As System.IO.StreamWriter
		Private markedBitmap As Integer()
		Private exportedBitmap As Integer()
		Private compoundKeyTypes As ClassDescriptor.FieldType()
	End Class
End Namespace
#End If
