Imports System.Collections
Imports Volante
Imports System.Diagnostics
Imports Link = Volante.ILink(Of IPersistent)
Namespace Volante.Impl

	Class RtreePage
		Inherits Persistent
		Const card As Integer = (Page.pageSize - ObjectHeader.Sizeof - 4 * 3) / (4 * 4 + 4)
		Const minFill As Integer = card / 2

		Friend n As Integer
		Friend b As Rectangle()
		Friend branch As Link

		Friend Sub New(db As IDatabase, obj As IPersistent, r As Rectangle)
			branch = db.CreateLink(Of IPersistent)(card)
			branch.Length = card
			b = New Rectangle(card - 1) {}
			setBranch(0, New Rectangle(r), obj)
			n = 1
			For i As Integer = 1 To card - 1
				b(i) = New Rectangle()
			Next
		End Sub

		Friend Sub New(db As IDatabase, root As RtreePage, p As RtreePage)
			branch = db.CreateLink(Of IPersistent)(card)
			branch.Length = card
			b = New Rectangle(card - 1) {}
			n = 2
			setBranch(0, root.cover(), root)
			setBranch(1, p.cover(), p)
			For i As Integer = 2 To card - 1
				b(i) = New Rectangle()
			Next
		End Sub

		Friend Sub New()
		End Sub

		Friend Function insert(db As IDatabase, r As Rectangle, obj As IPersistent, level As Integer) As RtreePage
			Modify()
			If System.Threading.Interlocked.Decrement(level) <> 0 Then
				' not leaf page
				Dim i As Integer, mini As Integer = 0
				Dim minIncr As Long = Long.MaxValue
				Dim minArea As Long = Long.MaxValue
				For i = 0 To n - 1
					Dim area As Long = b(i).Area()
					Dim incr As Long = Rectangle.JoinArea(b(i), r) - area
					If incr < minIncr Then
						minIncr = incr
						minArea = area
						mini = i
					ElseIf incr = minIncr AndAlso area < minArea Then
						minArea = area
						mini = i
					End If
				Next
				Dim p As RtreePage = DirectCast(branch(mini), RtreePage)
				Dim q As RtreePage = p.insert(db, r, obj, level)
				If q Is Nothing Then
					' child was not split
					b(mini).Join(r)
					Return Nothing
				Else
					' child was split
					setBranch(mini, p.cover(), p)
					Return addBranch(db, q.cover(), q)
				End If
			Else
				Return addBranch(db, New Rectangle(r), obj)
			End If
		End Function

		Friend Function remove(r As Rectangle, obj As IPersistent, level As Integer, reinsertList As ArrayList) As Integer
			If System.Threading.Interlocked.Decrement(level) <> 0 Then
				For i As Integer = 0 To n - 1
					If r.Intersects(b(i)) Then
						Dim pg As RtreePage = DirectCast(branch(i), RtreePage)
						Dim reinsertLevel As Integer = pg.remove(r, obj, level, reinsertList)
						If reinsertLevel >= 0 Then
							If pg.n >= minFill Then
								setBranch(i, pg.cover(), pg)
								Modify()
							Else
								' not enough entries in child
								reinsertList.Add(pg)
								reinsertLevel = level - 1
								removeBranch(i)
							End If
							Return reinsertLevel
						End If
					End If
				Next
			Else
				For i As Integer = 0 To n - 1
					If branch.ContainsElement(i, obj) Then
						removeBranch(i)
						Return 0
					End If
				Next
			End If
			Return -1
		End Function

		Friend Sub find(r As Rectangle, result As ArrayList, level As Integer)
			If System.Threading.Interlocked.Decrement(level) <> 0 Then
				' this is an internal node in the tree 
				For i As Integer = 0 To n - 1
					If r.Intersects(b(i)) Then
						DirectCast(branch(i), RtreePage).find(r, result, level)
					End If
				Next
			Else
				' this is a leaf node 
				For i As Integer = 0 To n - 1
					If r.Intersects(b(i)) Then
						result.Add(branch(i))
					End If
				Next
			End If
		End Sub

		Friend Sub purge(level As Integer)
			If System.Threading.Interlocked.Decrement(level) <> 0 Then
				' this is an internal node in the tree 
				For i As Integer = 0 To n - 1
					DirectCast(branch(i), RtreePage).purge(level)
				Next
			End If
			Deallocate()
		End Sub

		Private Sub setBranch(i As Integer, r As Rectangle, obj As IPersistent)
			b(i) = r
			branch(i) = obj
		End Sub

		Private Sub removeBranch(i As Integer)
			n -= 1
			Array.Copy(b, i + 1, b, i, n - i)
			branch.RemoveAt(i)
			branch.Length = card
			Modify()
		End Sub

		Private Function addBranch(db As IDatabase, r As Rectangle, obj As IPersistent) As RtreePage
			If n < card Then
				setBranch(System.Math.Max(System.Threading.Interlocked.Increment(n),n - 1), r, obj)
				Return Nothing
			Else
				Return splitPage(db, r, obj)
			End If
		End Function

		Private Function splitPage(db As IDatabase, r As Rectangle, obj As IPersistent) As RtreePage
			Dim i As Integer, j As Integer, seed0 As Integer = 0, seed1 As Integer = 0
			Dim rectArea As Long() = New Long(card) {}
			Dim waste As Long
			Dim worstWaste As Long = Long.MinValue
			'
			' As the seeds for the two groups, find two rectangles which waste 
			' the most area if covered by a single rectangle.
			'
			rectArea(0) = r.Area()
			For i = 0 To card - 1
				rectArea(i + 1) = b(i).Area()
			Next
			Dim bp As Rectangle = r
			For i = 0 To card - 1
				For j = i + 1 To card
					waste = Rectangle.JoinArea(bp, b(j - 1)) - rectArea(i) - rectArea(j)
					If waste > worstWaste Then
						worstWaste = waste
						seed0 = i
						seed1 = j
					End If
				Next
				bp = b(i)
			Next
			Dim taken As Byte() = New Byte(card - 1) {}
			Dim group0 As Rectangle, group1 As Rectangle
			Dim groupArea0 As Long, groupArea1 As Long
			Dim groupCard0 As Integer, groupCard1 As Integer
			Dim pg As RtreePage

			taken(seed1 - 1) = 2
			group1 = New Rectangle(b(seed1 - 1))

			If seed0 = 0 Then
				group0 = New Rectangle(r)
				pg = New RtreePage(db, obj, r)
			Else
				group0 = New Rectangle(b(seed0 - 1))
				pg = New RtreePage(db, branch.GetRaw(seed0 - 1), group0)
				setBranch(seed0 - 1, r, obj)
			End If
			groupCard0 = InlineAssignHelper(groupCard1, 1)
			groupArea0 = rectArea(seed0)
			groupArea1 = rectArea(seed1)
			'
			' Split remaining rectangles between two groups.
			' The one chosen is the one with the greatest difference in area 
			' expansion depending on which group - the rect most strongly 
			' attracted to one group and repelled from the other.
			'
			While groupCard0 + groupCard1 < card + 1 AndAlso groupCard0 < card + 1 - minFill AndAlso groupCard1 < card + 1 - minFill
				Dim betterGroup As Integer = -1, chosen As Integer = -1
				Dim biggestDiff As Long = -1
				For i = 0 To card - 1
					If taken(i) = 0 Then
						Dim diff As Long = (Rectangle.JoinArea(group0, b(i)) - groupArea0) - (Rectangle.JoinArea(group1, b(i)) - groupArea1)
						If diff > biggestDiff OrElse -diff > biggestDiff Then
							chosen = i
							If diff < 0 Then
								betterGroup = 0
								biggestDiff = -diff
							Else
								betterGroup = 1
								biggestDiff = diff
							End If
						End If
					End If
				Next
				Debug.Assert(chosen >= 0)
				If betterGroup = 0 Then
					group0.Join(b(chosen))
					groupArea0 = group0.Area()
					taken(chosen) = 1
					pg.setBranch(System.Math.Max(System.Threading.Interlocked.Increment(groupCard0),groupCard0 - 1), b(chosen), branch.GetRaw(chosen))
				Else
					groupCard1 += 1
					group1.Join(b(chosen))
					groupArea1 = group1.Area()
					taken(chosen) = 2
				End If
			End While
			'
			' If one group gets too full, then remaining rectangle are
			' split between two groups in such way to balance cards of two groups.
			'
			If groupCard0 + groupCard1 < card + 1 Then
				For i = 0 To card - 1
					If taken(i) = 0 Then
						If groupCard0 >= groupCard1 Then
							taken(i) = 2
							groupCard1 += 1
						Else
							taken(i) = 1
							pg.setBranch(System.Math.Max(System.Threading.Interlocked.Increment(groupCard0),groupCard0 - 1), b(i), branch.GetRaw(i))
						End If
					End If
				Next
			End If
			pg.n = groupCard0
			n = groupCard1
			i = 0
			j = 0
			While i < groupCard1
				If taken(j) = 2 Then
					setBranch(System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1), b(j), branch.GetRaw(j))
				End If
				j += 1
			End While
			Return pg
		End Function

		Friend Function cover() As Rectangle
			Dim r As New Rectangle(b(0))
			For i As Integer = 1 To n - 1
				r.Join(b(i))
			Next
			Return r
		End Function
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
