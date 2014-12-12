Imports System.Threading
Imports System.Collections
Namespace Volante

	''' <summary>Base class for persistent capable objects supporting locking
	''' </summary>
	Public Class PersistentResource
		Inherits Persistent
		Implements IResource
		#If CF Then
		Private Class WaitContext
			Friend evt As AutoResetEvent
			Friend [next] As WaitContext
			Friend exclusive As Boolean

			Friend Sub New()
				evt = New AutoResetEvent(False)
			End Sub
		End Class

		Shared freeContexts As WaitContext

		<NonSerialized> _
		Private queueStart As WaitContext
		<NonSerialized> _
		Private queueEnd As WaitContext

		Private Sub wait(exclusive As Boolean)
			Dim ctx As WaitContext
			SyncLock GetType(PersistentResource)
				ctx = freeContexts
				If ctx Is Nothing Then
					ctx = New WaitContext()
				Else
					freeContexts = ctx.[next]
				End If
				ctx.[next] = Nothing
			End SyncLock
			If queueStart IsNot Nothing Then
				queueEnd = InlineAssignHelper(queueEnd.[next], ctx)
			Else
				queueStart = InlineAssignHelper(queueEnd, ctx)
			End If
			ctx.exclusive = exclusive

			Monitor.[Exit](Me)
			ctx.evt.WaitOne()

			SyncLock GetType(PersistentResource)
				ctx.[next] = freeContexts
				freeContexts = ctx
			End SyncLock
		End Sub

		Public Sub SharedLock()
			Monitor.Enter(Me)
			Dim currThread As Thread = Thread.CurrentThread
			If owner Is currThread Then
				nWriters += 1
				Monitor.[Exit](Me)
			ElseIf nWriters = 0 Then
				nReaders += 1
				Monitor.[Exit](Me)
			Else
				wait(False)
			End If
		End Sub

		Public Sub ExclusiveLock()
			Dim currThread As Thread = Thread.CurrentThread
			Monitor.Enter(Me)
			If owner Is currThread Then
				nWriters += 1
				Monitor.[Exit](Me)
			ElseIf nReaders = 0 AndAlso nWriters = 0 Then
				nWriters = 1
				owner = currThread
				Monitor.[Exit](Me)
			Else
				wait(True)
				owner = currThread
			End If
		End Sub

		Private Sub notify()
			Dim [next] As WaitContext, ctx As WaitContext = queueStart
			While ctx IsNot Nothing
				If ctx.exclusive Then
					If nReaders = 0 Then
						nWriters = 1
						[next] = ctx.[next]
						ctx.evt.[Set]()
						ctx = [next]
					End If
					Exit While
				Else
					nReaders += 1
					[next] = ctx.[next]
					ctx.evt.[Set]()
					ctx = [next]
				End If
			End While
			queueStart = ctx
		End Sub

		Public Sub Unlock()
			SyncLock Me
				If nWriters <> 0 Then
					If System.Threading.Interlocked.Decrement(nWriters) = 0 Then
						owner = Nothing
						notify()
					End If
				ElseIf nReaders <> 0 Then
					If System.Threading.Interlocked.Decrement(nReaders) = 0 Then
						notify()
					End If
				End If
			End SyncLock
		End Sub

		Public Sub Reset()
			SyncLock Me
				nReaders = 0
				nWriters = 0
				owner = Nothing
				notify()
			End SyncLock
		End Sub
		#Else
		Public Sub SharedLock()
			SyncLock Me
				Dim currThread As Thread = Thread.CurrentThread
				While True
					If owner Is currThread Then
						nWriters += 1
						Exit While
					ElseIf nWriters = 0 Then
						If nReaders = 0 AndAlso db IsNot Nothing Then
							db.lockObject(Me)
						End If
						nReaders += 1
						Exit While
					Else
						Monitor.Wait(Me)
					End If
				End While
			End SyncLock
		End Sub

		Public Function SharedLock(timeout As Long) As Boolean
			Dim currThread As Thread = Thread.CurrentThread
			Dim startTime As DateTime = DateTime.Now
			Dim ts As TimeSpan = TimeSpan.FromMilliseconds(timeout)
			SyncLock Me
				While True
					If owner Is currThread Then
						nWriters += 1
						Return True
					ElseIf nWriters = 0 Then
						If nReaders = 0 AndAlso db IsNot Nothing Then
							db.lockObject(Me)
						End If
						nReaders += 1
						Return True
					Else
						Dim currTime As DateTime = DateTime.Now
						If startTime + ts <= currTime Then
							Return False
						End If
						Monitor.Wait(Me, startTime + ts - currTime)
					End If
				End While
			End SyncLock
		End Function


		Public Sub ExclusiveLock()
			Dim currThread As Thread = Thread.CurrentThread
			SyncLock Me
				While True
					If owner Is currThread Then
						nWriters += 1
						Exit While
					ElseIf nReaders = 0 AndAlso nWriters = 0 Then
						nWriters = 1
						owner = currThread
						If db IsNot Nothing Then
							db.lockObject(Me)
						End If
						Exit While
					Else
						Monitor.Wait(Me)
					End If
				End While
			End SyncLock
		End Sub

		Public Function ExclusiveLock(timeout As Long) As Boolean
			Dim currThread As Thread = Thread.CurrentThread
			Dim ts As TimeSpan = TimeSpan.FromMilliseconds(timeout)
			Dim startTime As DateTime = DateTime.Now
			SyncLock Me
				While True
					If owner Is currThread Then
						nWriters += 1
						Return True
					ElseIf nReaders = 0 AndAlso nWriters = 0 Then
						nWriters = 1
						owner = currThread
						If db IsNot Nothing Then
							db.lockObject(Me)
						End If
						Return True
					Else
						Dim currTime As DateTime = DateTime.Now
						If startTime + ts <= currTime Then
							Return False
						End If
						Monitor.Wait(Me, startTime + ts - currTime)
					End If
				End While
			End SyncLock
		End Function

		Public Sub Unlock()
			SyncLock Me
				If nWriters <> 0 Then
					If System.Threading.Interlocked.Decrement(nWriters) = 0 Then
						owner = Nothing
						Monitor.PulseAll(Me)
					End If
				ElseIf nReaders <> 0 Then
					If System.Threading.Interlocked.Decrement(nReaders) = 0 Then
						Monitor.PulseAll(Me)
					End If
				End If
			End SyncLock
		End Sub

		Public Sub Reset()
			SyncLock Me
				nReaders = 0
				nWriters = 0
				owner = Nothing
				Monitor.PulseAll(Me)
			End SyncLock
		End Sub

		#End If
		Protected Friend Sub New()
		End Sub

		Protected Friend Sub New(db As IDatabase)
			MyBase.New(db)
		End Sub

		<NonSerialized> _
		Private owner As Thread
		<NonSerialized> _
		Private nReaders As Integer
		<NonSerialized> _
		Private nWriters As Integer
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
