Imports System.Collections
Imports System.IO
Namespace Volante

	Public Class TestBlobResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public ReadTime As TimeSpan
	End Class

	Public Class TestBlob
		Implements ITest
		Private Shared Function IsSrcImpl(d As String) As String
			d = Path.Combine(d, "src")
			If Directory.Exists(d) Then
				Return Path.Combine(d, "impl")
			End If
			Return Nothing
		End Function

		Private Shared Function FindSrcImplDirectory() As String
			Dim curr As String = ""
			For i As Integer = 0 To 5
				Dim d As String = IsSrcImpl(curr)
				If d IsNot Nothing Then
					Return d
				End If
				curr = Path.Combine("..", curr)
			Next
			Return Nothing
		End Function

		Private Sub InsertFiles(config As TestConfig, files As String())
			Dim rc As Integer
			Dim buf As Byte() = New Byte(1023) {}
			Dim db As IDatabase = config.GetDatabase()
			Dim root As IIndex(Of String, IBlob) = DirectCast(db.Root, IIndex(Of String, IBlob))
			Tests.Assert(root Is Nothing)
			root = db.CreateIndex(Of String, IBlob)(IndexType.Unique)
			db.Root = root
			For Each file As String In files
				Dim fin As New FileStream(file, FileMode.Open, FileAccess.Read)
				Dim blob As IBlob = db.CreateBlob()
				Dim bout As Stream = blob.GetStream()
				While (InlineAssignHelper(rc, fin.Read(buf, 0, buf.Length))) > 0
					bout.Write(buf, 0, rc)
				End While
				root(file) = blob
				fin.Close()
				bout.Close()
			Next
			db.Close()
		End Sub

		Private Sub VerifyFiles(config As TestConfig, files As String())
			Dim rc As Integer
			Dim buf As Byte() = New Byte(1023) {}
			Dim db As IDatabase = config.GetDatabase(False)
			Dim root As IIndex(Of String, IBlob) = DirectCast(db.Root, IIndex(Of String, IBlob))
			Tests.Assert(root IsNot Nothing)
			For Each file As String In files
				Dim buf2 As Byte() = New Byte(1023) {}
				Dim blob As IBlob = root(file)
				Tests.Assert(blob IsNot Nothing)
				Dim bin As Stream = blob.GetStream()
				Dim fin As New FileStream(file, FileMode.Open, FileAccess.Read)
				While (InlineAssignHelper(rc, fin.Read(buf, 0, buf.Length))) > 0
					Dim rc2 As Integer = bin.Read(buf2, 0, buf2.Length)
					Tests.Assert(rc = rc2)
					If rc <> rc2 Then
						Exit While
					End If
					While System.Threading.Interlocked.Decrement(rc) >= 0 AndAlso buf(rc) = buf2(rc)
						

					End While
					Tests.Assert(rc < 0)
					If rc >= 0 Then
						Exit While
					End If
				End While
				fin.Close()
				bin.Close()
			Next
			db.Commit()
			db.Close()
		End Sub

		Public Sub TestBlobImpl(config As TestConfig)
			Dim n As Integer
			Dim db As IDatabase = config.GetDatabase(False)
			Dim blob As IBlob = db.CreateBlob()
			Dim blobStrm As Stream = blob.GetStream()

			Dim b As Byte() = New Byte() {1, 2, 3, 4, 5, 6}
			Dim b2 As Byte() = New Byte(5) {}
			blobStrm.Write(b, 0, b.Length)
			Tests.Assert(blobStrm.CanRead)
			Tests.Assert(blobStrm.CanSeek)
			Tests.Assert(blobStrm.CanWrite)
			Dim len As Long = 6
			Dim pos As Long = 3
			Tests.Assert(blobStrm.Length = len)
			blobStrm.Flush()
			Tests.Assert(6 = blobStrm.Position)
			blobStrm.Position = pos
			Tests.Assert(pos = blobStrm.Position)
			Tests.AssertException(Of ArgumentException)(Function() 
			blobStrm.Position = -1

End Function)
			blobStrm.Seek(0, SeekOrigin.Begin)
			Tests.Assert(0 = blobStrm.Position)
			n = blobStrm.Read(b2, 0, 6)
			Tests.Assert(n = 6)
			Tests.Assert(Tests.ByteArraysEqual(b, b2))
			Tests.Assert(6 = blobStrm.Position)
			n = blobStrm.Read(b2, 0, 1)
			Tests.Assert(n = 0)
			blobStrm.Seek(0, SeekOrigin.Begin)
			blobStrm.Seek(3, SeekOrigin.Current)
			Tests.Assert(3 = blobStrm.Position)
			blobStrm.Read(b2, 0, 3)
			Tests.Assert(6 = blobStrm.Position)
			Tests.Assert(b2(0) = 4)
			blobStrm.Seek(-3, SeekOrigin.[End])
			Tests.Assert(3 = blobStrm.Position)
			Tests.AssertException(Of ArgumentException)(Function() 
			blobStrm.Seek(-10, SeekOrigin.Current)

End Function)
			blobStrm.Seek(0, SeekOrigin.[End])
			Tests.Assert(len = blobStrm.Position)
			blobStrm.Write(b, 0, b.Length)
			len += b.Length
			Tests.Assert(blobStrm.Length = len)
			blobStrm.SetLength(8)
			Tests.Assert(blobStrm.Length = 8)
			blobStrm.SetLength(20)
			Tests.Assert(blobStrm.Length = 20)
			blob.Deallocate()
		End Sub

		Public Sub Run(config As TestConfig)
			Dim res = New TestBlobResult()
			config.Result = res
			Dim dir As String = FindSrcImplDirectory()
			If dir Is Nothing Then
				res.Ok = False
				Return
			End If
			Dim files As String() = Directory.GetFiles(dir, "*.cs")
			res.Count = files.Length

			Dim start = DateTime.Now
			InsertFiles(config, files)
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			VerifyFiles(config, files)
			res.ReadTime = DateTime.Now - start

			TestBlobImpl(config)
		End Sub
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class

End Namespace
