Imports System.IO
Namespace Volante

	Public Class TestBackupResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public BackupTime As TimeSpan
	End Class

	Public Class TestBackup
		Implements ITest
		Private Class Record
			Inherits Persistent
			Friend strKey As [String]
			Friend intKey As Long
			Friend realKey As Double
		End Class

		Private Class Root
			Inherits Persistent
			Friend strIndex As IIndex(Of String, Record)
			Friend intIndex As IFieldIndex(Of Long, Record)
			Friend compoundIndex As IMultiFieldIndex(Of Record)
		End Class

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim count As Integer = config.Count
			Dim res = New TestBackupResult()

			Dim start As DateTime = DateTime.Now

			Dim dbNameBackup As String = Convert.ToString(config.DatabaseName) & ".backup.dbs"
			Dim db As IDatabase = config.GetDatabase()
			Dim root As Root = DirectCast(db.Root, Root)
			Tests.Assert(root Is Nothing)
			root = New Root()
			root.strIndex = db.CreateIndex(Of String, Record)(IndexType.Unique)
			root.intIndex = db.CreateFieldIndex(Of Long, Record)("intKey", IndexType.Unique)
			root.compoundIndex = db.CreateFieldIndex(Of Record)(New [String]() {"strKey", "intKey"}, IndexType.Unique)
			db.Root = root
			Dim intIndex As IFieldIndex(Of Long, Record) = root.intIndex
			Dim compoundIndex As IMultiFieldIndex(Of Record) = root.compoundIndex
			Dim strIndex As IIndex(Of String, Record) = root.strIndex
			Dim key As Long = 1999
			For i = 0 To count - 1
				Dim rec As New Record()
				rec.intKey = key
				rec.strKey = System.Convert.ToString(key)
				rec.realKey = CDbl(key)
				intIndex.Put(rec)
				strIndex.Put(New Key(rec.strKey), rec)
				compoundIndex.Put(rec)
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
			Next
			db.Commit()
			Tests.Assert(intIndex.Count = count)
			Tests.Assert(strIndex.Count = count)
			Tests.Assert(compoundIndex.Count = count)
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			Dim stream As New System.IO.FileStream(dbNameBackup, FileMode.Create, FileAccess.Write)
			db.Backup(stream)
			stream.Close()
			db.Close()
			res.BackupTime = DateTime.Now - start

			start = DateTime.Now
			db.Open(dbNameBackup)
			root = DirectCast(db.Root, Root)
			intIndex = root.intIndex
			strIndex = root.strIndex
			compoundIndex = root.compoundIndex

			key = 1999
			For i = 0 To count - 1
				Dim strKey As [String] = System.Convert.ToString(key)
				Dim rec1 As Record = intIndex.[Get](key)
				Dim rec2 As Record = strIndex.[Get](strKey)
				Dim rec3 As Record = compoundIndex.[Get](New Key(strKey, key))

				Tests.Assert(rec1 IsNot Nothing)
				Tests.Assert(rec1 Is rec2)
				Tests.Assert(rec1 Is rec3)
				Tests.Assert(rec1.intKey = key)
				Tests.Assert(rec1.realKey = CDbl(key))
				Tests.Assert(strKey.Equals(rec1.strKey))
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
			Next
			db.Close()
		End Sub
	End Class
End Namespace
