Namespace Volante

	Public Class TestMultiFieldIndex
		Implements ITest
		Public Class Root
			Inherits Persistent
			Public idx As IMultiFieldIndex(Of RecordFullWithProperty)
			Public idxNonUnique As IMultiFieldIndex(Of RecordFullWithProperty)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestResult()
			config.Result = res

			Dim db As IDatabase = config.GetDatabase()
			Tests.Assert(db.Root Is Nothing)
			Dim root As New Root()

			Tests.AssertDatabaseException(Function() 
			root.idx = db.CreateFieldIndex(Of RecordFullWithProperty)(New String() {"NonExistent"}, IndexType.NonUnique)

End Function, DatabaseException.ErrorCode.INDEXED_FIELD_NOT_FOUND)

			root.idx = db.CreateFieldIndex(Of RecordFullWithProperty)(New String() {"Int64Prop", "StrVal"}, IndexType.Unique)
			root.idxNonUnique = db.CreateFieldIndex(Of RecordFullWithProperty)(New String() {"Int64Val", "ByteVal"}, IndexType.NonUnique)
			db.Root = root
			Tests.Assert(root.idx.IndexedClass = GetType(RecordFullWithProperty))
			Tests.Assert(root.idx.KeyField.Name = "Int64Prop")
			Tests.Assert(root.idx.KeyFields(1).Name = "StrVal")

			Tests.AssertDatabaseException(Function() 
			root.idx.Append(New RecordFullWithProperty(0))

End Function, DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE)

			Dim rFirst As RecordFullWithProperty = Nothing
			Dim firstkey As Long = 0
			For Each key As var In Tests.KeySeq(count)
				Dim rec As New RecordFullWithProperty(key)
				root.idx.Put(rec)
				root.idxNonUnique.Put(rec)
				If rFirst Is Nothing Then
					rFirst = rec
					firstkey = key
				End If
			Next
			Tests.Assert(root.idx.Count = count)
			db.Commit()
			Tests.Assert(root.idx.Count = count)
			Tests.Assert(rFirst.IsPersistent())

			Tests.Assert(root.idx.Contains(rFirst))
			Tests.Assert(root.idxNonUnique.Contains(rFirst))

			Dim rTmp = New RecordFullWithProperty(firstkey)
			Tests.Assert(Not root.idx.Contains(rTmp))
			Tests.Assert(Not root.idxNonUnique.Contains(rTmp))

			Dim recs As RecordFullWithProperty() = root.idx.ToArray()
			Tests.Assert(recs.Length = count)

			' TODO: figure out why Set() returns null
			Dim removed = root.idx.[Set](rTmp)
			'Tests.Assert(removed == rFirst);
			removed = root.idxNonUnique.[Set](rTmp)
			'Tests.Assert(removed == rFirst);

			Dim minKey As Long = Int32.MaxValue
			Dim maxKey As Long = Int32.MinValue
			For Each key As var In Tests.KeySeq(count)
				Dim strKey As [String] = Convert.ToString(key)
				Dim rec As RecordFullWithProperty = root.idx.[Get](New Key(New [Object]() {key, strKey}))
				Tests.Assert(rec IsNot Nothing AndAlso rec.Int64Val = key AndAlso rec.StrVal.Equals(strKey))
				If key < minKey Then
					minKey = key
				End If
				If key > maxKey Then
					maxKey = key
				End If
			Next

			Dim n As Integer = 0
			Dim prevStr As String = ""
			Dim prevInt As Long = minKey
			For Each rec As RecordFullWithProperty In root.idx.Range(New Key(minKey, ""), New Key(maxKey + 1, "???"), IterationOrder.AscentOrder)
				Tests.Assert(rec.Int64Val > prevInt OrElse rec.Int64Val = prevInt AndAlso rec.StrVal.CompareTo(prevStr) > 0)
				prevStr = rec.StrVal
				prevInt = rec.Int64Val
				n += 1
			Next
			Tests.Assert(n = count)

			n = 0
			prevInt = maxKey + 1
			For Each rec As RecordFullWithProperty In root.idx.Range(New Key(minKey, "", False), New Key(maxKey + 1, "???", False), IterationOrder.DescentOrder)
				Tests.Assert(rec.Int64Val < prevInt OrElse rec.Int64Val = prevInt AndAlso rec.StrVal.CompareTo(prevStr) < 0)
				prevStr = rec.StrVal
				prevInt = rec.Int64Val
				n += 1
			Next
			Tests.Assert(n = count)

			rFirst = root.idx.ToArray()(0)
			Tests.Assert(root.idx.Contains(rFirst))
			Tests.Assert(root.idx.Remove(rFirst))
			Tests.Assert(Not root.idx.Contains(rFirst))
			Tests.Assert(Not root.idx.Remove(rFirst))

			rFirst = root.idxNonUnique.ToArray()(0)
			Tests.Assert(root.idxNonUnique.Contains(rFirst))
			Tests.Assert(root.idxNonUnique.Remove(rFirst))
			Tests.Assert(Not root.idxNonUnique.Contains(rFirst))
			Tests.Assert(Not root.idxNonUnique.Remove(rFirst))

			For Each o As var In root.idx.ToArray()
				Dim key As Long = o.Int64Val
				Dim strKey As [String] = Convert.ToString(key)
				Dim rec As RecordFullWithProperty = root.idx.[Get](New Key(New [Object]() {key, strKey}))
				Tests.Assert(rec IsNot Nothing AndAlso rec.Int64Val = key AndAlso rec.StrVal.Equals(strKey))
				Tests.Assert(root.idx.Contains(rec))
				root.idx.Remove(rec)
			Next
			Tests.Assert(Not root.idx.GetEnumerator().MoveNext())
			Tests.Assert(Not root.idx.Reverse().GetEnumerator().MoveNext())

			db.Close()
		End Sub
	End Class
End Namespace
