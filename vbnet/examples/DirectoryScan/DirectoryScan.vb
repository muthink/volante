Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports Volante

Namespace DirectoryScan
	Class FileEntry
		Inherits Persistent
		Public Path As String
		Public Size As Long
		Public CreationTimeUtc As DateTime
		Public LastAccessTimeUtc As DateTime
		Public LastWriteTimeUtc As DateTime
	End Class

	Class DatabaseRoot
		Inherits Persistent
		Public FileSizeIndex As IIndex(Of Long, FileEntry)
		Public FileNameIndex As IIndex(Of String, FileEntry)
		Public FileLastWriteTimeIndex As IIndex(Of DateTime, FileEntry)
	End Class

	Class DirectoryScan
		Const LIMIT As Integer = 5

		Private Shared Sub Main(args As String())
			Dim db As IDatabase = DatabaseFactory.CreateDatabase()
			Dim dbName As String = "fileinfo.dbs"
			db.Open(dbName)
			Dim dbRoot As DatabaseRoot = Nothing
			If db.Root IsNot Nothing Then
				dbRoot = DirectCast(db.Root, DatabaseRoot)
			Else
				' only create root once
				dbRoot = New DatabaseRoot()
				dbRoot.FileSizeIndex = db.CreateIndex(Of Int64, FileEntry)(IndexType.NonUnique)
				dbRoot.FileNameIndex = db.CreateIndex(Of String, FileEntry)(IndexType.NonUnique)
				dbRoot.FileLastWriteTimeIndex = db.CreateIndex(Of DateTime, FileEntry)(IndexType.NonUnique)
				db.Root = dbRoot
				' changing the root marks database as modified but it's
				' only modified in memory. Commit to persist changes to disk.
				db.Commit()
				PopulateDatabase(db, "c:\")
			End If

			ListSmallestFiles(dbRoot, LIMIT)
			ListBiggestFiles(dbRoot, LIMIT)
			ListMostRecentlyWrittenToFiles(dbRoot, LIMIT)
			ListDuplicateNamesFiles(dbRoot, LIMIT)
			RemoveOneFileEntry(db, dbRoot)
			db.Close()
		End Sub

		' change showMemoryStatsto true to see object stats on the console
		' before and after removal
		Private Shared Sub RemoveOneFileEntry(db As IDatabase, dbRoot As DatabaseRoot, Optional showMemoryStats As Boolean = False)
			If showMemoryStats Then
				Console.WriteLine("Memory stats before removal:")
				DumpMemoryUsage(db.GetMemoryUsage().Values)
			End If
			' we pick one object and remove it from all 3 indexes
			If dbRoot.FileSizeIndex.Count = 0 Then
				Return
			End If
			Dim toRemove As FileEntry = Nothing
			For Each fe As var In dbRoot.FileSizeIndex
				toRemove = fe
				Exit For
			Next
			' Remove an object with a given key from all 3 indexes.
			' We still need to provide the object because those are
			' non-unique indexes, so the same key might point to many
			' objects and we only want to remove this specific object.
			Dim name As String = Path.GetFileName(toRemove.Path)
			dbRoot.FileNameIndex.Remove(name, toRemove)
			dbRoot.FileSizeIndex.Remove(toRemove.Size, toRemove)
			dbRoot.FileLastWriteTimeIndex.Remove(toRemove.LastWriteTimeUtc, toRemove)
			' changes are not reflected in the database until we commit
			db.Commit()
			If showMemoryStats Then
				Console.WriteLine("Memory stats after removal:")
				DumpMemoryUsage(db.GetMemoryUsage().Values)
			End If
		End Sub

		Private Shared Sub PopulateDatabase(db As IDatabase, startDir As String)
			Dim dbRoot As DatabaseRoot = DirectCast(db.Root, DatabaseRoot)
			' scan all directories starting with startDir
			Dim dirsToVisit = New List(Of String)() From { _
				startDir _
			}
			Dim insertedCount As Integer = 0
			While dirsToVisit.Count > 0
				Dim dirPath = dirsToVisit(0)
				dirsToVisit.RemoveAt(0)
				' accessing directory information might fail e.g. if we
				' don't have access permissions so we'll skip all 
				Try
					Dim dirInfo As New DirectoryInfo(dirPath)
					For Each di As var In dirInfo.EnumerateDirectories()
						dirsToVisit.Add(di.FullName)
					Next
					For Each fi As var In dirInfo.EnumerateFiles()
						Dim fe = New FileEntry() With { _
							Key .Path = fi.FullName, _
							Key .Size = fi.Length, _
							Key .CreationTimeUtc = fi.CreationTimeUtc, _
							Key .LastAccessTimeUtc = fi.LastAccessTimeUtc, _
							Key .LastWriteTimeUtc = fi.LastWriteTimeUtc _
						}
						Dim name As String = Path.GetFileName(fe.Path)
						dbRoot.FileSizeIndex.Put(fe.Size, fe)
						dbRoot.FileNameIndex.Put(name, fe)
						dbRoot.FileLastWriteTimeIndex.Put(fe.LastWriteTimeUtc, fe)
						insertedCount += 1
						If insertedCount Mod 10000 = 0 Then
							Console.WriteLine([String].Format("Inserted {0} FileEntry objects", insertedCount))
							db.Commit()
						End If
					Next
				Catch
				End Try
			End While
			' commit the changes if we're done creating a database
			db.Commit()
			' when we're finished, each index should have the same
			' number of items in it, equal to number of inserted objects
			Debug.Assert(dbRoot.FileSizeIndex.Count = insertedCount)
			Debug.Assert(dbRoot.FileNameIndex.Count = insertedCount)
			Debug.Assert(dbRoot.FileLastWriteTimeIndex.Count = insertedCount)
		End Sub

		Private Shared Sub ListSmallestFiles(dbRoot As DatabaseRoot, limit As Integer)
			Console.WriteLine(vbLf & "The smallest files:")
			' Indexes are ordered in ascending order i.e. smallest
			' values are first. Index implements GetEnumerator()
			' function which we can (implicitly) use in foreach loop:
			For Each fe As var In dbRoot.FileSizeIndex
				Console.WriteLine([String].Format("{0}: {1} bytes", fe.Path, fe.Size))
				If System.Threading.Interlocked.Decrement(limit) = 0 Then
					Exit For
				End If
			Next
		End Sub

		Private Shared Sub ListBiggestFiles(dbRoot As DatabaseRoot, limit As Integer)
			Console.WriteLine(vbLf & "The biggest files:")
			' To list biggest files, we iterate the index in descending
			' order, using an enumerator returned by Reverse() function:
			For Each fe As var In dbRoot.FileSizeIndex.Reverse()
				Console.WriteLine([String].Format("{0}: {1} bytes", fe.Path, fe.Size))
				If System.Threading.Interlocked.Decrement(limit) = 0 Then
					Exit For
				End If
			Next
		End Sub

		Private Shared Sub ListMostRecentlyWrittenToFiles(dbRoot As DatabaseRoot, limit As Integer)
			Console.WriteLine(vbLf & "The most recently written-to files:")
			' the biggest DateTime values represent the most recent dates,
			' so once again we iterate the index in reverse (descent) order:
			For Each fe As var In dbRoot.FileLastWriteTimeIndex.Reverse()
				Console.WriteLine([String].Format("{0}: {1} bytes", fe.Path, fe.Size))
				If System.Threading.Interlocked.Decrement(limit) = 0 Then
					Exit For
				End If
			Next
		End Sub

		Private Shared Sub ListDuplicateNamesFiles(dbRoot As DatabaseRoot, limit As Integer)
			Console.WriteLine(vbLf & "Files with the same name:")
			Dim prevName As String = ""
			Dim prevPath As String = ""
			' The name of the file is not an explicit part of FileEntry
			' object, but since it's part of the index, we can access it
			' if we use IDictionaryEnumerator, which provides both the
			' key and 
			Dim de As IDictionaryEnumerator = dbRoot.FileNameIndex.GetDictionaryEnumerator()
			Dim dups = New Dictionary(Of String, Boolean)()
			While de.MoveNext()
				Dim name As String = DirectCast(de.Key, String)
				Dim fe As FileEntry = DirectCast(de.Value, FileEntry)
				If name = prevName Then
					Dim firstDup As Boolean = Not dups.ContainsKey(name)
					If firstDup Then
						Console.WriteLine(prevPath)
						Console.WriteLine(" " & fe.Path)
						dups(name) = True
						If System.Threading.Interlocked.Decrement(limit) = 0 Then
							Exit While
						End If
					Else
						Console.WriteLine(" " & fe.Path)
					End If
				End If
				prevName = name
				prevPath = fe.Path
			End While
		End Sub

		Public Shared Sub DumpMemoryUsage(usages As ICollection(Of TypeMemoryUsage))
			Console.WriteLine("Memory usage")
			For Each usage As TypeMemoryUsage In usages
				Console.WriteLine((((" " + usage.Type.Name & ": instances=") + usage.Count & ", total size=") + usage.TotalSize & ", allocated size=") + usage.AllocatedSize)
			Next
		End Sub

	End Class
End Namespace
