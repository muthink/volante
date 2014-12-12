Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestTtreeResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public IndexSearchTime As TimeSpan
		Public RemoveTime As TimeSpan
	End Class

	Public Class TestTtree
		Implements ITest
		Private Class Name
			Public first As [String]
			Public last As [String]

			Public Sub New()
			End Sub

			Public Sub New(p As Person)
				first = p.first
				last = p.last
			End Sub

			Public Sub New(key As Long)
				Dim str As [String] = Convert.ToString(key)
				Dim m As Integer = str.Length \ 2
				Me.first = str.Substring(0, m)
				Me.last = str.Substring(m)
			End Sub
		End Class

		Private Class Person
			Inherits Persistent
			Public first As [String]
			Public last As [String]
			Public age As Integer

			Private Sub New()
			End Sub
			Public Sub New(key As Long)
				Dim str As [String] = Convert.ToString(key)
				Dim m As Integer = str.Length \ 2
				Me.first = str.Substring(0, m)
				Me.last = str.Substring(m)
				Me.age = CInt(key) Mod 100
			End Sub

			Public Sub New(firstName As [String], lastName As [String], age As Integer)
				Me.first = firstName
				Me.last = lastName
				Me.age = age
			End Sub
		End Class

		Private Class PersonList
			Inherits Persistent
			Public list As ISortedCollection(Of Name, Person)
		End Class

		Private Class NameComparator
			Inherits PersistentComparator(Of Name, Person)
			Public Overrides Function CompareMembers(p1 As Person, p2 As Person) As Integer
				Dim diff As Integer = p1.first.CompareTo(p2.first)
				If diff <> 0 Then
					Return diff
				End If
				Return p1.last.CompareTo(p2.last)
			End Function

			Public Overrides Function CompareMemberWithKey(p As Person, name As Name) As Integer
				Dim diff As Integer = p.first.CompareTo(name.first)
				If diff <> 0 Then
					Return diff
				End If
				Return p.last.CompareTo(name.last)
			End Function
		End Class

		Private Sub PopulateIndex(list As ISortedCollection(Of Name, Person), count As Integer)
			Dim firstPerson As Person = Nothing
			For Each key As var In Tests.KeySeq(count)
				Dim p As New Person(key)
				If firstPerson Is Nothing Then
					firstPerson = p
				End If
				list.Add(p)
			Next
			list.Add(firstPerson)
		End Sub

		Public Sub Run(config As TestConfig)
			Dim i As Integer
			Dim count As Integer = config.Count
			Dim res = New TestTtreeResult()
			Dim start As DateTime = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()
			Dim root As PersonList = DirectCast(db.Root, PersonList)
			Tests.Assert(root Is Nothing)
			root = New PersonList()
			root.list = db.CreateSortedCollection(Of Name, Person)(New NameComparator(), IndexType.Unique)
			db.Root = root
			Dim list As ISortedCollection(Of Name, Person) = root.list
			Tests.Assert(Not list.IsReadOnly)
			Tests.Assert(Not list.RecursiveLoading())
			PopulateIndex(list, count)
			db.Commit()
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			For Each key As var In Tests.KeySeq(count)
				Dim name As New Name(key)
				Dim age As Integer = CInt(key) Mod 100

				Dim p As Person = list(name)
				Tests.Assert(p IsNot Nothing)
				Tests.Assert(list.Contains(p))
				Tests.Assert(p.age = age)
			Next
			res.IndexSearchTime = DateTime.Now - start

			start = DateTime.Now
			Dim nm As New Name()
			nm.first = InlineAssignHelper(nm.last, "")
			Dim comparator As PersistentComparator(Of Name, Person) = list.GetComparator()
			i = 0
			For Each p As Person In list
				Tests.Assert(comparator.CompareMemberWithKey(p, nm) > 0)
				nm.first = p.first
				nm.last = p.last
				Tests.Assert(list.Remove(p))
				i += 1
			Next
			Tests.Assert(i = count)
			res.RemoveTime = DateTime.Now - start
			Tests.Assert(list.Count = 0)
			PopulateIndex(list, count)

			Dim els As Person() = list.ToArray()
			Tests.Assert(els.Length = list.Count)
			Dim firstKey As New Name(els(0))
			Dim lastKey As New Name(els(els.Length - 1))
			Dim midKey As New Name(els(els.Length \ 2))
			Dim els2 As Person() = list(firstKey, lastKey)
			Tests.Assert(els.Length = els2.Length)
			Dim e = list.Range(firstKey, midKey).GetEnumerator()
			TestEnumerator(e)
			e = list.GetEnumerator(midKey, lastKey)
			TestEnumerator(e)

			For Each key As var In Tests.KeySeq(count)
				Dim p = RemoveEl(els, key)
				Tests.Assert(list.Contains(p))
				Tests.Assert(list.Remove(p))
				Tests.Assert(Not list.Contains(p))
			Next
			Tests.Assert(list.[Get](firstKey) Is Nothing)
			Tests.Assert(list.[Get](New Name(-123345)) Is Nothing)
			db.Commit()
			PopulateIndex(list, 20)
			Tests.Assert(20 = list.Count)
			Tests.Assert(list.[Get](New Name(-123456)) Is Nothing)
			Dim arr = list.ToArray()
			Tests.Assert(20 = arr.Length)
			Dim pTmp As Person = arr(0)
			list.Clear()
			Tests.Assert(Not list.Remove(pTmp))
			list.Deallocate()
			db.Commit()
			db.Close()
		End Sub

		Private Sub TestEnumerator(e As IEnumerator(Of Person))
			While e.MoveNext()
				Tests.Assert(e.Current IsNot Nothing)
			End While
			Tests.Assert(Not e.MoveNext())
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim el = e.Current

End Function)
			e.Reset()
			Tests.Assert(e.MoveNext())
		End Sub

		Private Function RemoveEl(els As Person(), key As Long) As Person
			Dim pos As Integer = CInt(key Mod els.Length)
			While els(pos) Is Nothing
				pos = (pos + 1) Mod els.Length
			End While
			Dim ret As Person = els(pos)
			els(pos) = Nothing
			Return ret
		End Function
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
