Namespace Volante

	Public Class TestFieldIndex
		Implements ITest
		Public Class RecordAuto
			Inherits Persistent
			Public IntAuto As Integer
			Public Property LongAuto() As Long
				Get
					Return m_LongAuto
				End Get
				Set
					m_LongAuto = Value
				End Set
			End Property
			Private m_LongAuto As Long
			Public BoolNoAuto As Boolean
		End Class

		Public Class Root
			Inherits Persistent
			Public idxBool As IFieldIndex(Of Boolean, RecordFullWithProperty)
			Public idxByte As IFieldIndex(Of Byte, RecordFullWithProperty)
			Public idxSByte As IFieldIndex(Of SByte, RecordFullWithProperty)
			Public idxShort As IFieldIndex(Of Short, RecordFullWithProperty)
			Public idxUShort As IFieldIndex(Of UShort, RecordFullWithProperty)
			Public idxInt As IFieldIndex(Of Integer, RecordFullWithProperty)
			Public idxUInt As IFieldIndex(Of UInteger, RecordFullWithProperty)
			Public idxLong As IFieldIndex(Of Long, RecordFullWithProperty)
			Public idxLongProp As IFieldIndex(Of Long, RecordFullWithProperty)
			Public idxULong As IFieldIndex(Of ULong, RecordFullWithProperty)
			' TODO: Btree.allocateRootPage() doesn't support tpChar even though 
			' FieldIndex does support it as a key and OldBtree supports it.
			'public IFieldIndex<char, RecordFullWithProperty> idxChar;
			Public idxFloat As IFieldIndex(Of Single, RecordFullWithProperty)
			Public idxDouble As IFieldIndex(Of Double, RecordFullWithProperty)
			Public idxDate As IFieldIndex(Of DateTime, RecordFullWithProperty)
			Public idxDecimal As IFieldIndex(Of Decimal, RecordFullWithProperty)
			Public idxGuid As IFieldIndex(Of Guid, RecordFullWithProperty)
			Public idxString As IFieldIndex(Of String, RecordFullWithProperty)
			' TODO: Btree.allocateRootPage() doesn't support tpEnum even though 
			' FieldIndex does support it as a key and OldBtree supports it.
			'public IFieldIndex<RecordFullWithPropertyEnum, RecordFullWithProperty> idxEnum;
			' TODO: OldBtree doesn't support oid as an index
			'public IFieldIndex<object, RecordFullWithProperty> idxObject;
			Public idxOid As IFieldIndex(Of Integer, RecordFullWithProperty)

			Public idxIntAuto As IFieldIndex(Of Integer, RecordAuto)
			Public idxLongAuto As IFieldIndex(Of Long, RecordAuto)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestResult()
			config.Result = res

			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim root As New Root()
			Tests.AssertDatabaseException(Function() 
			root.idxBool = db.CreateFieldIndex(Of Boolean, RecordFullWithProperty)("NonExistent", IndexType.NonUnique)

End Function, DatabaseException.ErrorCode.INDEXED_FIELD_NOT_FOUND)

			Tests.AssertDatabaseException(Function() 
			root.idxBool = db.CreateFieldIndex(Of Boolean, RecordFullWithProperty)("CharVal", IndexType.NonUnique)

End Function, DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)

			root.idxBool = db.CreateFieldIndex(Of Boolean, RecordFullWithProperty)("BoolVal", IndexType.NonUnique)
			root.idxByte = db.CreateFieldIndex(Of Byte, RecordFullWithProperty)("ByteVal", IndexType.NonUnique)
			root.idxSByte = db.CreateFieldIndex(Of SByte, RecordFullWithProperty)("SByteVal", IndexType.NonUnique)
			root.idxShort = db.CreateFieldIndex(Of Short, RecordFullWithProperty)("Int16Val", IndexType.NonUnique)
			root.idxUShort = db.CreateFieldIndex(Of UShort, RecordFullWithProperty)("UInt16Val", IndexType.NonUnique)
			root.idxInt = db.CreateFieldIndex(Of Integer, RecordFullWithProperty)("Int32Val", IndexType.NonUnique)
			root.idxUInt = db.CreateFieldIndex(Of UInteger, RecordFullWithProperty)("UInt32Val", IndexType.NonUnique)
			root.idxLong = db.CreateFieldIndex(Of Long, RecordFullWithProperty)("Int64Val", IndexType.Unique)
			root.idxLongProp = db.CreateFieldIndex(Of Long, RecordFullWithProperty)("Int64Prop", IndexType.Unique)
			root.idxULong = db.CreateFieldIndex(Of ULong, RecordFullWithProperty)("UInt64Val", IndexType.NonUnique)
			'root.idxChar = db.CreateFieldIndex<char, RecordFullWithProperty>("CharVal", IndexType.NonUnique);
			root.idxFloat = db.CreateFieldIndex(Of Single, RecordFullWithProperty)("FloatVal", IndexType.NonUnique)
			root.idxDouble = db.CreateFieldIndex(Of Double, RecordFullWithProperty)("DoubleVal", IndexType.NonUnique)
			root.idxDate = db.CreateFieldIndex(Of DateTime, RecordFullWithProperty)("DateTimeVal", IndexType.NonUnique)
			root.idxDecimal = db.CreateFieldIndex(Of [Decimal], RecordFullWithProperty)("DecimalVal", IndexType.NonUnique)
			root.idxGuid = db.CreateFieldIndex(Of Guid, RecordFullWithProperty)("GuidVal", IndexType.NonUnique)
			root.idxString = db.CreateFieldIndex(Of [String], RecordFullWithProperty)("StrVal", IndexType.NonUnique)
			'root.idxEnum = db.CreateFieldIndex<RecordFullWithPropertyEnum, RecordFullWithProperty>("EnumVal", IndexType.NonUnique);
			'root.idxObject = db.CreateFieldIndex<object, RecordFullWithProperty>("ObjectVal", IndexType.NonUnique);
			root.idxOid = db.CreateFieldIndex(Of Integer, RecordFullWithProperty)("Oid", IndexType.NonUnique)

			root.idxIntAuto = db.CreateFieldIndex(Of Integer, RecordAuto)("IntAuto", IndexType.Unique)
			root.idxLongAuto = db.CreateFieldIndex(Of Long, RecordAuto)("LongAuto", IndexType.Unique)
			db.Root = root

			Tests.Assert(root.idxString.IndexedClass = GetType(RecordFullWithProperty))
			Tests.Assert(root.idxString.KeyField.Name = "StrVal")

			Dim i As Integer = 0
			Dim rfFirst As RecordFullWithProperty = Nothing
			Dim raFirst As RecordAuto = Nothing
			Dim firstKey As Long = 0
			For Each key As Long In Tests.KeySeq(count)
				Dim r = New RecordFullWithProperty(key)
				root.idxBool.Put(r)
				root.idxByte.Put(r)
				root.idxSByte.Put(r)
				root.idxShort.Put(r)
				root.idxUShort.Put(r)
				root.idxInt.Put(r)
				root.idxUInt.Put(r)
				root.idxLong.Put(r)
				root.idxLongProp.Put(r)
				root.idxULong.Put(r)
				'root.idxChar.Put(r);
				root.idxFloat.Put(r)
				root.idxDouble.Put(r)
				root.idxDate.Put(r)
				root.idxDecimal.Put(r)
				root.idxGuid.Put(r)
				root.idxString.Put(r)
				'root.idxEnum.Put(r);
				'root.idxObject.Put(r);
				root.idxOid.Put(r)

				Dim ra = New RecordAuto()
				root.idxIntAuto.Append(ra)
				root.idxLongAuto.Append(ra)
				Tests.Assert(ra.IntAuto = i)
				Tests.Assert(ra.LongAuto = i)
				i += 1
				If rfFirst Is Nothing Then
					rfFirst = r
					raFirst = ra
					firstKey = key
				End If
			Next
			db.Commit()
			Dim r2 = New RecordFullWithProperty(firstKey)
			' Contains for unique index
			Tests.Assert(root.idxLong.Contains(rfFirst))
			Tests.Assert(Not root.idxLong.Contains(r2))

			' Contains() for non-unique index
			Tests.Assert(root.idxInt.Contains(rfFirst))
			Tests.Assert(Not root.idxInt.Contains(r2))

			Tests.Assert(False = root.idxLongProp.Put(r2))
			root.idxLongProp.[Set](r2)

			Tests.AssertDatabaseException(Function() 
			root.idxString.Append(rfFirst)

End Function, DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE)

			Tests.Assert(root.idxBool.Remove(rfFirst))
			Tests.Assert(root.idxByte.Remove(rfFirst))
			Tests.Assert(root.idxSByte.Remove(rfFirst))
			Tests.Assert(root.idxShort.Remove(rfFirst))
			Tests.Assert(root.idxUShort.Remove(rfFirst))
			Tests.Assert(root.idxInt.Remove(rfFirst))
			Tests.Assert(root.idxUInt.Remove(rfFirst))
			Tests.Assert(root.idxLong.Remove(rfFirst))
			Tests.Assert(root.idxULong.Remove(rfFirst))
			Tests.Assert(root.idxFloat.Remove(rfFirst))
			Tests.Assert(root.idxDouble.Remove(rfFirst))
			Tests.Assert(root.idxDate.Remove(rfFirst))
			Tests.Assert(root.idxDecimal.Remove(rfFirst))
			Tests.Assert(root.idxGuid.Remove(rfFirst))
			Tests.Assert(root.idxString.Remove(rfFirst))
			db.Commit()
			Tests.Assert(Not root.idxBool.Remove(rfFirst))
			Tests.Assert(Not root.idxByte.Remove(rfFirst))
			Tests.Assert(Not root.idxSByte.Remove(rfFirst))
			Tests.Assert(Not root.idxShort.Remove(rfFirst))
			Tests.Assert(Not root.idxUShort.Remove(rfFirst))
			Tests.Assert(Not root.idxInt.Remove(rfFirst))
			Tests.Assert(Not root.idxUInt.Remove(rfFirst))
			Tests.Assert(Not root.idxLong.Remove(rfFirst))
			Tests.Assert(Not root.idxLongProp.Remove(rfFirst))
			Tests.Assert(Not root.idxULong.Remove(rfFirst))
			Tests.Assert(Not root.idxFloat.Remove(rfFirst))
			Tests.Assert(Not root.idxDouble.Remove(rfFirst))
			Tests.Assert(Not root.idxDate.Remove(rfFirst))
			Tests.Assert(Not root.idxDecimal.Remove(rfFirst))
			Tests.Assert(Not root.idxGuid.Remove(rfFirst))
			Tests.Assert(Not root.idxString.Remove(rfFirst))
			db.Commit()
			Dim e = root.idxLong.GetEnumerator()
			Tests.Assert(e.MoveNext())
			r2 = e.Current
			Tests.Assert(root.idxLongProp.Remove(r2))
			db.Commit()
			Tests.Assert(Not root.idxLongProp.Remove(r2))
		End Sub
	End Class
End Namespace
