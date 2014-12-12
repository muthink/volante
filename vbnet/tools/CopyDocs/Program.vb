Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.IO

Namespace CopyDocs
	Class Program
		' we expect our source code is checked out under
		' "volante" directory
		Private Shared Function FindSrcRooDir() As String
			Dim path__1 = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
			Dim parts = path__1.Split(New Char(0) {Path.DirectorySeparatorChar})
			Dim n As Integer = parts.Length
			While n > 0
				If parts(System.Threading.Interlocked.Decrement(n)) = "volante" Then
					' hack to work around bug (?) in Path.Combine() which
					' combines "C:", "foo" as "c:foo" and not "c:\foo"
					Dim d = parts(0) & Path.DirectorySeparatorChar
					Dim r = New String(n - 2) {}
					Array.Copy(parts, 1, r, 0, r.Length)
					For Each rp As var In r
						d = Path.Combine(d, rp)
					Next
					EnsureDirExists(d)
					Return d
				End If
			End While
			Throw New Exception("Couldn't find directory")
		End Function

		Private Shared Sub EnsureDirExists(dir As String)
			If Not Directory.Exists(dir) Then
				Throw New Exception([String].Format("Dir {0} doesn't exist", dir))
			End If
		End Sub

		Private Shared Function FindSrcDocsDir() As String
			Dim path__1 = FindSrcRooDir()
			path__1 = Path.Combine(path__1, "volante", "csharp", "doc")
			EnsureDirExists(path__1)
			Return path__1
		End Function

		Private Shared Function FindDstDocsDir() As String
			Dim path__1 = FindSrcRooDir()
			path__1 = Path.Combine(path__1, "web", "blog", "www", "software")
			EnsureDirExists(path__1)
			path__1 = Path.Combine(path__1, "volante")
			Dim pathTmp = Path.Combine(path__1, "js")
			If Not Directory.Exists(pathTmp) Then
				Directory.CreateDirectory(pathTmp)
			End If
			EnsureDirExists(path__1)
			Return path__1
		End Function

		Private Shared Sub DeleteFilesInDir(dir As String)
			For Each path As var In Directory.GetFiles(dir)
				File.Delete(path)
			Next
		End Sub

		Private Shared Function ShouldCopy(fileName As String) As Boolean
			Dim ext = Path.GetExtension(fileName).ToLower()
			Return ext = ".html" OrElse ext = ".css" OrElse ext = ".js"
		End Function

		Shared FileNameSubst As New Dictionary(Of String, String)() From { _
			{"index.html", "database.html"} _
		}

		Shared StrSubst As New Dictionary(Of String, String)() From { _
			{"<span id=gplus></span>", "<span style='position:relative; left: 22px; top: 6px;'>" & vbCr & vbLf & vbTab & vbTab & "<script type='text/javascript' src='http://apis.google.com/js/plusone.js'></script>" & vbCr & vbLf & vbTab & vbTab & "<g:plusone size='medium' href='http://blog.kowalczyk.info/software/volante/'>" & vbCr & vbLf & vbTab & vbTab & "</g:plusone>" & vbCr & vbLf & vbTab & vbTab & "</span>"}, _
			{"<span id=adsense></span>", "<script type='text/javascript'> " & vbCr & vbLf & "  var _gaq = _gaq || [];" & vbCr & vbLf & "  _gaq.push(['_setAccount', 'UA-194516-1']);" & vbCr & vbLf & "  _gaq.push(['_trackPageview']);" & vbCr & vbLf & " " & vbCr & vbLf & "  (function() {" & vbCr & vbLf & "    var ga = document.createElement('script'); ga.type = 'text/javascript'; ga.async = true;" & vbCr & vbLf & "    ga.src = ('https:' == document.location.protocol ? 'https://ssl' : 'http://www') + '.google-analytics.com/ga.js';" & vbCr & vbLf & "    (document.getElementsByTagName('head')[0] || document.getElementsByTagName('body')[0]).appendChild(ga);" & vbCr & vbLf & "  })();" & vbCr & vbLf & "</script> "}, _
			{"href=""index.html""", "href=""database.html"""}, _
			{"href=index.html", "href=""database.html"""} _
		}

		Private Shared Function NoSubst(path__1 As String) As Boolean
			Dim ext = Path.GetExtension(path__1).ToLower()
			Return ext = ".css" OrElse ext = ".js"
		End Function

		Private Shared Sub CopyFile(srcPath As String, dstPath As String)
			If NoSubst(srcPath) Then
				File.Copy(srcPath, dstPath)
				Return
			End If

			Dim contentOrig As String = File.ReadAllText(srcPath)
			Dim content As String = contentOrig
			For Each strOld As var In StrSubst.Keys
				Dim strNew = StrSubst(strOld)
				content = content.Replace(strOld, strNew)
			Next

			If content = contentOrig Then
				File.Copy(srcPath, dstPath)
			Else
				File.WriteAllText(dstPath, content, Encoding.UTF8)
			End If
		End Sub

		Private Shared Sub CopyFilesInDir(srcDir As String, dstDir As String)
			Dim srcFiles = Directory.GetFiles(srcDir)
			For Each filePath As var In srcFiles
				Dim fileName = Path.GetFileName(filePath)
				If Not ShouldCopy(fileName) Then
					Continue For
				End If
				Dim srcPath = Path.Combine(srcDir, fileName)
				Dim dstFileName As String = Nothing
				If Not FileNameSubst.TryGetValue(fileName, dstFileName) Then
					dstFileName = fileName
				End If
				Dim dstPath = Path.Combine(dstDir, dstFileName)
				CopyFile(srcPath, dstPath)
				Console.WriteLine([String].Format("{0} =>" & vbLf & "{1}" & vbLf, srcPath, dstPath))
			Next
		End Sub

		Private Shared Sub Main(args As String())
			Dim srcDir As String = FindSrcDocsDir()
			Dim dstDir As String = FindDstDocsDir()
			DeleteFilesInDir(dstDir)
			' we want a clean slate - no leaving of obsolete files
			CopyFilesInDir(srcDir, dstDir)
			srcDir = Path.Combine(srcDir, "js")
			dstDir = Path.Combine(dstDir, "js")
			DeleteFilesInDir(dstDir)
			CopyFilesInDir(srcDir, dstDir)
		End Sub
	End Class
End Namespace
