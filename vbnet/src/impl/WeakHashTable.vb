Imports Volante
Namespace Volante.Impl

	Public Class WeakHashTable
		Inherits OidHashTable
		Friend table As Entry()
		Friend Const loadFactor As Single = 0.75F
		Friend count As Integer
		Friend threshold As Integer

		Public Sub New(initialCapacity As Integer)
			threshold = CInt(Math.Truncate(initialCapacity * loadFactor))
			table = New Entry(initialCapacity - 1) {}
		End Sub

		Public Function Remove(oid As Integer) As Boolean
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index), prev As Entry = Nothing
				While e IsNot Nothing
					If e.oid = oid Then
						If prev IsNot Nothing Then
							prev.[next] = e.[next]
						Else
							tab(index) = e.[next]
						End If
						e.clear()
						count -= 1
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
						e.oref.Target = obj
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
				tab(index) = New Entry(oid, New WeakReference(obj), tab(index))
				count += 1
			End SyncLock
		End Sub

		Public Function [Get](oid As Integer) As IPersistent
			While True
				SyncLock Me
					Dim tab As Entry() = table
					Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
					Dim e As Entry = tab(index), prev As Entry = Nothing
					While e IsNot Nothing
						If e.oid = oid Then
							Dim obj As IPersistent = DirectCast(e.oref.Target, IPersistent)
							If obj Is Nothing Then
								If e.dirty > 0 Then
									GoTo waitFinalization
								End If
							ElseIf obj.IsDeleted() Then
								If prev IsNot Nothing Then
									prev.[next] = e.[next]
								Else
									tab(index) = e.[next]
								End If
								e.clear()
								count -= 1
								Return Nothing
							End If
							Return obj
						End If
						prev = e
						e = e.[next]
					End While
					Return Nothing
				End SyncLock
				waitFinalization:
				GC.WaitForPendingFinalizers()
			End While
		End Function

		Friend Sub rehash()
			Dim oldCapacity As Integer = table.Length
			Dim oldMap As Entry() = table
			Dim i As Integer
			i = oldCapacity
			While System.Threading.Interlocked.Decrement(i) >= 0
				Dim e As Entry, [next] As Entry, prev As Entry
				prev = Nothing
				e = oldMap(i)
				While e IsNot Nothing
					[next] = e.[next]
					If Not e.oref.IsAlive AndAlso e.dirty = 0 Then
						count -= 1
						e.clear()
						If prev Is Nothing Then
							oldMap(i) = [next]
						Else
							prev.[next] = [next]
						End If
					Else
						prev = e
					End If
					e = [next]
				End While
			End While

			If CUInt(count) <= (CUInt(threshold) >> 1) Then
				Return
			End If
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

		Public Sub Flush()
			While True
				SyncLock Me
					For i As Integer = 0 To table.Length - 1
						Dim e As Entry = table(i)
						While e IsNot Nothing
							Dim obj As IPersistent = DirectCast(e.oref.Target, IPersistent)
							If obj IsNot Nothing Then
								If obj.IsModified() Then
									obj.Store()
								End If
							ElseIf e.dirty <> 0 Then
								GoTo waitFinalization
							End If
							e = e.[next]
						End While
					Next
					Return
				End SyncLock
				waitFinalization:
				GC.WaitForPendingFinalizers()
			End While
		End Sub

		Public Sub Invalidate()
			While True
				SyncLock Me
					For i As Integer = 0 To table.Length - 1
						Dim e As Entry = table(i)
						While e IsNot Nothing
							Dim obj As IPersistent = DirectCast(e.oref.Target, IPersistent)
							If obj IsNot Nothing Then
								If obj.IsModified() Then
									e.dirty = 0
									obj.Invalidate()
								End If
							ElseIf e.dirty <> 0 Then
								GoTo waitFinalization
							End If
							e = e.[next]
						End While
						table(i) = Nothing
					Next
					count = 0
					Return
				End SyncLock
				waitFinalization:
				GC.WaitForPendingFinalizers()
			End While
		End Sub

		Public Sub SetDirty(oid As Integer)
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index)
				While e IsNot Nothing
					If e.oid = oid Then
						e.dirty += 1
						Return
					End If
					e = e.[next]
				End While
			End SyncLock
		End Sub

		Public Sub ClearDirty(oid As Integer)
			SyncLock Me
				Dim tab As Entry() = table
				Dim index As Integer = (oid And &H7fffffff) Mod tab.Length
				Dim e As Entry = tab(index), prev As Entry = Nothing
				While e IsNot Nothing
					If e.oid = oid Then
						If e.oref.IsAlive Then
							If e.dirty > 0 Then
								e.dirty -= 1
							End If
						Else
							If prev IsNot Nothing Then
								prev.[next] = e.[next]
							Else
								tab(index) = e.[next]
							End If
							e.clear()
							count -= 1
						End If
						Return
					End If
					prev = e
					e = e.[next]
				End While
			End SyncLock
		End Sub

		Friend Class Entry
			Friend [next] As Entry
			Friend oref As WeakReference
			Friend oid As Integer
			Friend dirty As Integer

			Friend Sub clear()
				oref.Target = Nothing
				oref = Nothing
				dirty = 0
				[next] = Nothing
			End Sub

			Friend Sub New(oid As Integer, oref As WeakReference, chain As Entry)
				[next] = chain
				Me.oid = oid
				Me.oref = oref
			End Sub
		End Class
	End Class
End Namespace
