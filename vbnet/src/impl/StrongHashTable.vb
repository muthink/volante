Imports Volante
Namespace Volante.Impl

	Public Class StrongHashTable
		Inherits OidHashTable
		Friend table As Entry()
		Friend Const loadFactor As Single = 0.75F
		Friend count As Integer
		Friend threshold As Integer

		Public Sub New(initialCapacity As Integer)
			threshold = CInt(Math.Truncate(initialCapacity * loadFactor))
			If initialCapacity <> 0 Then
				table = New Entry(initialCapacity - 1) {}
			End If
		End Sub

		Public Function Remove(oid As Integer) As Boolean
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index), prev As Entry = Nothing
				While e IsNot Nothing
					If e.oid = oid Then
						e.oref = Nothing
						count -= 1
						If prev IsNot Nothing Then
							prev.[next] = e.[next]
						Else
							tab(index) = e.[next]
						End If
						Return True
					End If
					prev = e
					e = e.[next]
				End While
				Return False
			End SyncLock
		End Function

		Public Sub Put(oid As Integer, obj As IPersistent)
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index)
				While e IsNot Nothing
					If e.oid = oid Then
						e.oref = obj
						Return
					End If
					e = e.[next]
				End While
				If count >= threshold Then
					' Rehash the table if the threshold is exceeded
					rehash()
					tab = table
					index = (oid And &H7fffffff) Mod tab.Length
				End If

				' Creates the new entry.
				tab(index) = New Entry(oid, obj, tab(index))
				count += 1
			End SyncLock
		End Sub

		Public Function [Get](oid As Integer) As IPersistent
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index)
				While e IsNot Nothing
					If e.oid = oid Then
						Return e.oref
					End If
					e = e.[next]
				End While
				Return Nothing
			End SyncLock
		End Function

		Public Sub Flush()
			SyncLock Me
				For i As Integer = 0 To table.Length - 1
					Dim e As Entry = table(i)
					While e IsNot Nothing
						If e.oref.IsModified() Then
							e.oref.Store()
						End If
						e = e.[next]
					End While
				Next
			End SyncLock
		End Sub

		Public Sub Invalidate()
			SyncLock Me
				For i As Integer = 0 To table.Length - 1
					Dim e As Entry = table(i)
					While e IsNot Nothing
						If e.oref.IsModified() Then
							e.oref.Invalidate()
						End If
						e = e.[next]
					End While
					table(i) = Nothing
				Next
				count = 0
			End SyncLock
		End Sub

		Friend Sub rehash()
			Dim oldCapacity As Integer = table.Length
			Dim oldMap As Entry() = table
			Dim i As Integer

			Dim newCapacity As Integer = oldCapacity * 2 + 1
			Dim newMap As Entry() = New Entry(newCapacity - 1) {}

			threshold = CInt(Math.Truncate(newCapacity * loadFactor))
			table = newMap

			i = oldCapacity
			While System.Threading.Interlocked.Decrement(i) >= 0
				Dim old As Entry = oldMap(i)
				While old IsNot Nothing
					Dim e As Entry = old
					old = old.[next]

					Dim index As Integer = (e.oid And &H7fffffff) Mod newCapacity
					e.[next] = newMap(index)
					newMap(index) = e
				End While
			End While
		End Sub

		Public Sub SetDirty(oid As Integer)
		End Sub

		Public Sub ClearDirty(oid As Integer)
		End Sub

		Friend Class Entry
			Friend [next] As Entry
			Friend oref As IPersistent
			Friend oid As Integer

			Friend Sub New(oid As Integer, oref As IPersistent, chain As Entry)
				[next] = chain
				Me.oid = oid
				Me.oref = oref
			End Sub
		End Class
	End Class
End Namespace
