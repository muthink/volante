#If WITH_XML Then
Namespace Volante

	Public Class TestXmlResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public ExportTime As TimeSpan
		Public ImportTime As TimeSpan
		Public IndexSearchTime As TimeSpan
	End Class

	Public Class TestXml
		Implements ITest
		Private Class Record
			Inherits Persistent
			Friend strKey As [String]
			Friend intKey As Long
			Friend realKey As Double
		End Class

		Private Structure Point
			Public x As Integer
			Public y As Integer
		End Structure

		Private Class Root
			Inherits Persistent
			Friend strIndex As IIndex(Of String, Record)
			Friend intIndex As IFieldIndex(Of Long, Record)
			Friend compoundIndex As IMultiFieldIndex(Of Record)
			Friend point As Point
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestXmlResult()
			config.Result = res

			Dim start As DateTime = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Dim xmlName As String = Convert.ToString(config.DatabaseName) & ".xml"
			Dim dbNameImported As String = Convert.ToString(config.DatabaseName) & ".imported.dbs"
			Tests.TryDeleteFile(xmlName)
			Tests.TryDeleteFile(dbNameImported)

			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIndex = db.CreateIndex(Of String, Record)(IndexType.Unique)
			root.intIndex = db.CreateFieldIndex(Of Long, Record)("intKey", IndexType.Unique)
			root.compoundIndex = db.CreateFieldIndex(Of Record)(New String() {"strKey", "intKey"}, IndexType.Unique)
			root.point.x = 1
			root.point.y = 2
			db.Root = root

			Dim strIndex As IIndex(Of String, Record) = root.strIndex
			Dim intIndex As IFieldIndex(Of Long, Record) = root.intIndex
			Dim compoundIndex As IMultiFieldIndex(Of Record) = root.compoundIndex

			Dim key As Long = 1999
			Dim i As Integer
			For i = 0 To count - 1
				Dim rec As New Record()
				rec.intKey = key
				rec.strKey = System.Convert.ToString(key)
				rec.realKey = CDbl(key)
				strIndex.Put(New Key(rec.strKey), rec)
				intIndex.Put(rec)
				compoundIndex.Put(rec)
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
			Next
			db.Commit()
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			Dim writer As New System.IO.StreamWriter(xmlName)
			db.ExportXML(writer)
			writer.Close()
			db.Close()
			res.ExportTime = DateTime.Now - start

			start = DateTime.Now
			db.Open(dbNameImported)
			Dim reader As New System.IO.StreamReader(xmlName)
			db.ImportXML(reader)
			reader.Close()
			res.ImportTime = DateTime.Now - start

			start = DateTime.Now

			root = DirectCast(db.Root, Root)
			strIndex = root.strIndex
			intIndex = root.intIndex
			compoundIndex = root.compoundIndex
			Tests.Assert(root.point.x = 1 AndAlso root.point.y = 2)

			key = 1999
			For i = 0 To count - 1
				Dim strKey As [String] = System.Convert.ToString(key)
				Dim rec1 As Record = strIndex(strKey)
				Dim rec2 As Record = intIndex(key)
				Dim rec3 As Record = compoundIndex.[Get](New Key(strKey, key))
				Tests.Assert(rec1 IsNot Nothing)
				Tests.Assert(rec1 Is rec2)
				Tests.Assert(rec1 Is rec3)
				Tests.Assert(rec1.intKey = key)
				Tests.Assert(rec1.realKey = CDbl(key))
				Tests.Assert(strKey.Equals(rec1.strKey))
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
			Next
			res.IndexSearchTime = DateTime.Now - start
			db.Close()
		End Sub
	End Class
End Namespace
#End If
