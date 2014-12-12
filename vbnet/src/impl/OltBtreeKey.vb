#If WITH_OLD_BTREE Then
Imports Volante
Imports System.Diagnostics
Namespace Volante.Impl

	Class OldBtreeKey
		Friend key As Key
		Friend oid As Integer
		Friend oldOid As Integer

		Friend Sub New(key As Key, oid As Integer)
			Me.key = key
			Me.oid = oid
		End Sub

		Friend Sub getStr(pg As Page, i As Integer)
			Dim len As Integer = OldBtreePage.getKeyStrSize(pg, i)
			Dim offs As Integer = OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, i)
			Dim sval As Char() = New Char(len - 1) {}
			For j As Integer = 0 To len - 1
				sval(j) = CChar(Bytes.unpack2(pg.data, offs))
				offs += 2
			Next
			key = New Key(sval)
		End Sub

		Friend Sub getByteArray(pg As Page, i As Integer)
			Dim len As Integer = OldBtreePage.getKeyStrSize(pg, i)
			Dim offs As Integer = OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, i)
			Dim bval As Byte() = New Byte(len - 1) {}
			Array.Copy(pg.data, offs, bval, 0, len)
			key = New Key(bval)
		End Sub

		Friend Sub extract(pg As Page, offs As Integer, type As ClassDescriptor.FieldType)
			Dim data As Byte() = pg.data

			Select Case type
				Case ClassDescriptor.FieldType.tpBoolean
					key = New Key(data(offs) <> 0)
					Exit Select

				Case ClassDescriptor.FieldType.tpSByte
					key = New Key(CSByte(data(offs)))
					Exit Select
				Case ClassDescriptor.FieldType.tpByte
					key = New Key(data(offs))
					Exit Select

				Case ClassDescriptor.FieldType.tpShort
					key = New Key(Bytes.unpack2(data, offs))
					Exit Select
				Case ClassDescriptor.FieldType.tpUShort
					key = New Key(CUShort(Bytes.unpack2(data, offs)))
					Exit Select

				Case ClassDescriptor.FieldType.tpChar
					key = New Key(CChar(Bytes.unpack2(data, offs)))
					Exit Select

				Case ClassDescriptor.FieldType.tpInt
					key = New Key(Bytes.unpack4(data, offs))
					Exit Select
				Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
					key = New Key(CUInt(Bytes.unpack4(data, offs)))
					Exit Select

				Case ClassDescriptor.FieldType.tpLong
					key = New Key(Bytes.unpack8(data, offs))
					Exit Select
				Case ClassDescriptor.FieldType.tpDate, ClassDescriptor.FieldType.tpULong
					key = New Key(CULng(Bytes.unpack8(data, offs)))
					Exit Select

				Case ClassDescriptor.FieldType.tpFloat
					key = New Key(Bytes.unpackF4(data, offs))
					Exit Select

				Case ClassDescriptor.FieldType.tpDouble
					key = New Key(Bytes.unpackF8(data, offs))
					Exit Select

				Case ClassDescriptor.FieldType.tpGuid
					key = New Key(Bytes.unpackGuid(data, offs))
					Exit Select

				Case ClassDescriptor.FieldType.tpDecimal
					key = New Key(Bytes.unpackDecimal(data, offs))
					Exit Select
				Case Else

					Debug.Assert(False, "Invalid type")
					Exit Select

			End Select
		End Sub

		Friend Sub pack(pg As Page, i As Integer)
			Dim dst As Byte() = pg.data
			Select Case key.type
				Case ClassDescriptor.FieldType.tpBoolean, ClassDescriptor.FieldType.tpSByte, ClassDescriptor.FieldType.tpByte
					dst(OldBtreePage.firstKeyOffs + i) = CByte(key.ival)
					Exit Select

				Case ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort, ClassDescriptor.FieldType.tpChar
					Bytes.pack2(dst, OldBtreePage.firstKeyOffs + i * 2, CShort(key.ival))
					Exit Select

				Case ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpOid
					Bytes.pack4(dst, OldBtreePage.firstKeyOffs + i * 4, key.ival)
					Exit Select

				Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong, ClassDescriptor.FieldType.tpDate
					Bytes.pack8(dst, OldBtreePage.firstKeyOffs + i * 8, key.lval)
					Exit Select

				Case ClassDescriptor.FieldType.tpFloat
					Bytes.packF4(dst, OldBtreePage.firstKeyOffs + i * 4, CSng(key.dval))
					Exit Select

				Case ClassDescriptor.FieldType.tpDouble
					Bytes.packF8(dst, OldBtreePage.firstKeyOffs + i * 8, key.dval)
					Exit Select

				Case ClassDescriptor.FieldType.tpDecimal
					Bytes.packDecimal(dst, OldBtreePage.firstKeyOffs + i * 16, key.dec)
					Exit Select

				Case ClassDescriptor.FieldType.tpGuid
					Bytes.packGuid(dst, OldBtreePage.firstKeyOffs + i * 16, key.guid)
					Exit Select
				Case Else


					Debug.Assert(False, "Invalid type")
					Exit Select

			End Select
			Bytes.pack4(dst, OldBtreePage.firstKeyOffs + (OldBtreePage.maxItems - i - 1) * 4, oid)
		End Sub
	End Class
End Namespace
#End If
