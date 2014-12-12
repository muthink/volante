Imports System.Collections
Imports System.Collections.Generic
Namespace Volante.Impl

	Class TtreePage(Of K, V As {Class, IPersistent})
		Inherits Persistent
		Const maxItems As Integer = (Page.pageSize - ObjectHeader.Sizeof - 4 * 5) / 4
		Const minItems As Integer = maxItems - 2
		' minimal number of items in internal node
		Private left As TtreePage(Of K, V)
		Private right As TtreePage(Of K, V)
		Private balance As Integer
		Private nItems As Integer
		Private item As V()

		Public Overrides Function RecursiveLoading() As Boolean
			Return False
		End Function

		Private Sub New()
		End Sub

		Friend Sub New(mbr As V)
			nItems = 1
			item = New V(maxItems - 1) {}
			item(0) = mbr
		End Sub

		Private Function loadItem(i As Integer) As V
			Dim mbr As V = item(i)
			mbr.Load()
			Return mbr
		End Function

		Friend Function find(comparator As PersistentComparator(Of K, V), minValue As K, minBoundary As BoundaryKind, maxValue As K, maxBoundary As BoundaryKind, selection As List(Of V)) As Boolean
			Dim l As Integer, r As Integer, m As Integer, n As Integer
			Load()
			n = nItems
			If minBoundary <> BoundaryKind.None Then
				If -comparator.CompareMemberWithKey(loadItem(0), minValue) >= CInt(minBoundary) Then
					If -comparator.CompareMemberWithKey(loadItem(n - 1), minValue) >= CInt(minBoundary) Then
						If right IsNot Nothing Then
							Return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection)
						End If
						Return True
					End If
					l = 0
					r = n
					While l < r
						m = (l + r) >> 1
						If -comparator.CompareMemberWithKey(loadItem(m), minValue) >= CInt(minBoundary) Then
							l = m + 1
						Else
							r = m
						End If
					End While
					While r < n
						If maxBoundary <> BoundaryKind.None AndAlso comparator.CompareMemberWithKey(loadItem(r), maxValue) >= CInt(maxBoundary) Then
							Return False
						End If
						selection.Add(loadItem(r))
						r += 1
					End While
					If right IsNot Nothing Then
						Return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection)
					End If
					Return True
				End If
			End If
			If left IsNot Nothing Then
				If Not left.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection) Then
					Return False
				End If
			End If
			For l = 0 To n - 1
				If maxBoundary <> BoundaryKind.None AndAlso comparator.CompareMemberWithKey(loadItem(l), maxValue) >= CInt(maxBoundary) Then
					Return False
				End If
				selection.Add(loadItem(l))
			Next
			If right IsNot Nothing Then
				Return right.find(comparator, minValue, minBoundary, maxValue, maxBoundary, selection)
			End If
			Return True
		End Function

		Friend Function contains(comparator As PersistentComparator(Of K, V), mbr As V) As Boolean
			Dim l As Integer, r As Integer, m As Integer, n As Integer
			Load()
			n = nItems
			If comparator.CompareMembers(loadItem(0), mbr) < 0 Then
				If comparator.CompareMembers(loadItem(n - 1), mbr) < 0 Then
					If right IsNot Nothing Then
						Return right.contains(comparator, mbr)
					End If
					Return False
				End If
				l = 0
				r = n
				While l < r
					m = (l + r) >> 1
					If comparator.CompareMembers(loadItem(m), mbr) < 0 Then
						l = m + 1
					Else
						r = m
					End If
				End While
				While r < n
					If mbr = loadItem(r) Then
						Return True
					End If

					If comparator.CompareMembers(item(r), mbr) > 0 Then
						Return False
					End If
					r += 1
				End While
				If right IsNot Nothing Then
					Return right.contains(comparator, mbr)
				End If
				Return False
			End If
			If left IsNot Nothing Then
				If left.contains(comparator, mbr) Then
					Return True
				End If
			End If
			For l = 0 To n - 1
				If mbr = loadItem(l) Then
					Return True
				End If
				If comparator.CompareMembers(item(l), mbr) > 0 Then
					Return False
				End If
			Next
			If right IsNot Nothing Then
				Return right.contains(comparator, mbr)
			End If
			Return False
		End Function

		Friend Const OK As Integer = 0
		Friend Const NOT_UNIQUE As Integer = 1
		Friend Const NOT_FOUND As Integer = 2
		Friend Const OVERFLOW As Integer = 3
		Friend Const UNDERFLOW As Integer = 4

		Friend Function insert(comparator As PersistentComparator(Of K, V), mbr As V, unique As Boolean, ByRef pgRef As TtreePage(Of K, V)) As Integer
			Dim pg As TtreePage(Of K, V), lp As TtreePage(Of K, V), rp As TtreePage(Of K, V)
			Dim reinsertItem As V
			Load()
			Dim n As Integer = nItems
			Dim diff As Integer = comparator.CompareMembers(mbr, loadItem(0))
			If diff <= 0 Then
				If unique AndAlso diff = 0 Then
					Return NOT_UNIQUE
				End If

				If (left Is Nothing OrElse diff = 0) AndAlso n <> maxItems Then
					Modify()
					'for (int i = n; i > 0; i--) item[i] = item[i-1];
					Array.Copy(item, 0, item, 1, n)
					item(0) = mbr
					nItems += 1
					Return OK
				End If
				If left Is Nothing Then
					Modify()
					left = New TtreePage(Of K, V)(mbr)
				Else
					pg = pgRef
					pgRef = left
					Dim result As Integer = left.insert(comparator, mbr, unique, pgRef)
					If result = NOT_UNIQUE Then
						Return NOT_UNIQUE
					End If

					Modify()
					left = pgRef
					pgRef = pg
					If result = OK Then
						Return OK
					End If
				End If
				If balance > 0 Then
					balance = 0
					Return OK
				ElseIf balance = 0 Then
					balance = -1
					Return OVERFLOW
				Else
					lp = Me.left
					lp.Load()
					lp.Modify()
					If lp.balance < 0 Then
						' single LL turn
						Me.left = lp.right
						lp.right = Me
						balance = 0
						lp.balance = 0
						pgRef = lp
					Else
						' double LR turn
						rp = lp.right
						rp.Load()
						rp.Modify()
						lp.right = rp.left
						rp.left = lp
						Me.left = rp.right
						rp.right = Me
						balance = If((rp.balance < 0), 1, 0)
						lp.balance = If((rp.balance > 0), -1, 0)
						rp.balance = 0
						pgRef = rp
					End If
					Return OK
				End If
			End If
			diff = comparator.CompareMembers(mbr, loadItem(n - 1))
			If diff >= 0 Then
				If unique AndAlso diff = 0 Then
					Return NOT_UNIQUE
				End If

				If (right Is Nothing OrElse diff = 0) AndAlso n <> maxItems Then
					Modify()
					item(n) = mbr
					nItems += 1
					Return OK
				End If
				If right Is Nothing Then
					Modify()
					right = New TtreePage(Of K, V)(mbr)
				Else
					pg = pgRef
					pgRef = right
					Dim result As Integer = right.insert(comparator, mbr, unique, pgRef)
					If result = NOT_UNIQUE Then
						Return NOT_UNIQUE
					End If

					Modify()
					right = pgRef
					pgRef = pg
					If result = OK Then
						Return OK
					End If
				End If
				If balance < 0 Then
					balance = 0
					Return OK
				ElseIf balance = 0 Then
					balance = 1
					Return OVERFLOW
				Else
					rp = Me.right
					rp.Load()
					rp.Modify()
					If rp.balance > 0 Then
						' single RR turn
						Me.right = rp.left
						rp.left = Me
						balance = 0
						rp.balance = 0
						pgRef = rp
					Else
						' double RL turn
						lp = rp.left
						lp.Load()
						lp.Modify()
						rp.left = lp.right
						lp.right = rp
						Me.right = lp.left
						lp.left = Me
						balance = If((lp.balance > 0), -1, 0)
						rp.balance = If((lp.balance < 0), 1, 0)
						lp.balance = 0
						pgRef = lp
					End If
					Return OK
				End If
			End If
			Dim l As Integer = 1, r As Integer = n - 1
			While l < r
				Dim i As Integer = (l + r) >> 1
				diff = comparator.CompareMembers(mbr, loadItem(i))
				If diff > 0 Then
					l = i + 1
				Else
					r = i
					If diff = 0 Then
						If unique Then
							Return NOT_UNIQUE
						End If
						Exit While
					End If
				End If
			End While
			' Insert before item[r]
			Modify()
			If n <> maxItems Then
				Array.Copy(item, r, item, r + 1, n - r)
				'for (int i = n; i > r; i--) item[i] = item[i-1]; 
				item(r) = mbr
				nItems += 1
				Return OK
			Else
				If balance >= 0 Then
					reinsertItem = loadItem(0)
					Array.Copy(item, 1, item, 0, r - 1)
					'for (int i = 1; i < r; i++) item[i-1] = item[i]; 
					item(r - 1) = mbr
				Else
					reinsertItem = loadItem(n - 1)
					Array.Copy(item, r, item, r + 1, n - r - 1)
					'for (int i = n-1; i > r; i--) item[i] = item[i-1]; 
					item(r) = mbr
				End If
				Return insert(comparator, reinsertItem, unique, pgRef)
			End If
		End Function

		Friend Function balanceLeftBranch(ByRef pgRef As TtreePage(Of K, V)) As Integer
			Dim lp As TtreePage(Of K, V), rp As TtreePage(Of K, V)
			If balance < 0 Then
				balance = 0
				Return UNDERFLOW
			ElseIf balance = 0 Then
				balance = 1
				Return OK
			Else
				rp = Me.right
				rp.Load()
				rp.Modify()
				If rp.balance >= 0 Then
					' single RR turn
					Me.right = rp.left
					rp.left = Me
					If rp.balance = 0 Then
						Me.balance = 1
						rp.balance = -1
						pgRef = rp
						Return OK
					Else
						balance = 0
						rp.balance = 0
						pgRef = rp
						Return UNDERFLOW
					End If
				Else
					' double RL turn
					lp = rp.left
					lp.Load()
					lp.Modify()
					rp.left = lp.right
					lp.right = rp
					Me.right = lp.left
					lp.left = Me
					balance = If(lp.balance > 0, -1, 0)
					rp.balance = If(lp.balance < 0, 1, 0)
					lp.balance = 0
					pgRef = lp
					Return UNDERFLOW
				End If
			End If
		End Function

		Friend Function balanceRightBranch(ByRef pgRef As TtreePage(Of K, V)) As Integer
			Dim lp As TtreePage(Of K, V), rp As TtreePage(Of K, V)
			If balance > 0 Then
				balance = 0
				Return UNDERFLOW
			ElseIf balance = 0 Then
				balance = -1
				Return OK
			Else
				lp = Me.left
				lp.Load()
				lp.Modify()
				If lp.balance <= 0 Then
					' single LL turn
					Me.left = lp.right
					lp.right = Me
					If lp.balance = 0 Then
						balance = -1
						lp.balance = 1
						pgRef = lp
						Return OK
					Else
						balance = 0
						lp.balance = 0
						pgRef = lp
						Return UNDERFLOW
					End If
				Else
					' double LR turn
					rp = lp.right
					rp.Load()
					rp.Modify()
					lp.right = rp.left
					rp.left = lp
					Me.left = rp.right
					rp.right = Me
					balance = If(rp.balance < 0, 1, 0)
					lp.balance = If(rp.balance > 0, -1, 0)
					rp.balance = 0
					pgRef = rp
					Return UNDERFLOW
				End If
			End If
		End Function

		Friend Function remove(comparator As PersistentComparator(Of K, V), mbr As V, ByRef pgRef As TtreePage(Of K, V)) As Integer
			Dim pg As TtreePage(Of K, V), [next] As TtreePage(Of K, V), prev As TtreePage(Of K, V)
			Load()
			Dim n As Integer = nItems
			Dim diff As Integer = comparator.CompareMembers(mbr, loadItem(0))
			If diff <= 0 Then
				If left IsNot Nothing Then
					Modify()
					pg = pgRef
					pgRef = left
					Dim h As Integer = left.remove(comparator, mbr, pgRef)
					left = pgRef
					pgRef = pg
					If h = UNDERFLOW Then
						Return balanceLeftBranch(pgRef)
					ElseIf h = OK Then
						Return OK
					End If
				End If
			End If
			diff = comparator.CompareMembers(mbr, loadItem(n - 1))
			If diff <= 0 Then
				For i As Integer = 0 To n - 1
					If item(i) = mbr Then
						If n = 1 Then
							If right Is Nothing Then
								Deallocate()
								pgRef = left
								Return UNDERFLOW
							ElseIf left Is Nothing Then
								Deallocate()
								pgRef = right
								Return UNDERFLOW
							End If
						End If
						Modify()
						If n <= minItems Then
							If left IsNot Nothing AndAlso balance <= 0 Then
								prev = left
								prev.Load()
								While prev.right IsNot Nothing
									prev = prev.right
									prev.Load()
								End While
								Array.Copy(item, 0, item, 1, i)
								'while (--i >= 0) 
								'{ 
								'    item[i+1] = item[i];
								'}
								item(0) = prev.item(prev.nItems - 1)
								pg = pgRef
								pgRef = left
								Dim h As Integer = left.remove(comparator, loadItem(0), pgRef)
								left = pgRef
								pgRef = pg
								If h = UNDERFLOW Then
									h = balanceLeftBranch(pgRef)
								End If
								Return h
							ElseIf right IsNot Nothing Then
								[next] = right
								[next].Load()
								While [next].left IsNot Nothing
									[next] = [next].left
									[next].Load()
								End While
								Array.Copy(item, i + 1, item, i, n - i - 1)
								'while (++i < n) 
								'{ 
								'    item[i-1] = item[i];
								'}
								item(n - 1) = [next].item(0)
								pg = pgRef
								pgRef = right
								Dim h As Integer = right.remove(comparator, loadItem(n - 1), pgRef)
								right = pgRef
								pgRef = pg
								If h = UNDERFLOW Then
									h = balanceRightBranch(pgRef)
								End If
								Return h
							End If
						End If
						Array.Copy(item, i + 1, item, i, n - i - 1)
						'while (++i < n) 
						'{ 
						'    item[i-1] = item[i];
						'}
						item(n - 1) = Nothing
						nItems -= 1
						Return OK
					End If
				Next
			End If
			If right IsNot Nothing Then
				Modify()
				pg = pgRef
				pgRef = right
				Dim h As Integer = right.remove(comparator, mbr, pgRef)
				right = pgRef
				pgRef = pg
				If h = UNDERFLOW Then
					Return balanceRightBranch(pgRef)
				Else
					Return h
				End If
			End If
			Return NOT_FOUND
		End Function

		Friend Function toArray(arr As IPersistent(), index As Integer) As Integer
			Load()
			If left IsNot Nothing Then
				index = left.toArray(arr, index)
			End If
			Dim i As Integer = 0, n As Integer = nItems
			While i < n
				arr(System.Math.Max(System.Threading.Interlocked.Increment(index),index - 1)) = loadItem(i)
				i += 1
			End While
			If right IsNot Nothing Then
				index = right.toArray(arr, index)
			End If
			Return index
		End Function

		Friend Sub prune()
			Load()
			If left IsNot Nothing Then
				left.prune()
			End If
			If right IsNot Nothing Then
				right.prune()
			End If
			Deallocate()
		End Sub
	End Class
End Namespace
