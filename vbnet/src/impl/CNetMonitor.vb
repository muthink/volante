Imports System.Threading

Namespace Volante.Impl
	''' <summary>
	''' Enhanced alternative to the <see cref="T:System.Threading.Monitor"/> class.  Provides a mechanism that synchronizes access to objects.
	''' </summary>
	''' <seealso cref="T:System.Threading.Monitor"/>
	Public NotInheritable Class CNetMonitor
		''' <summary>
		''' The owner of the monitor, or null if it's not owned
		''' by any thread.
		''' </summary>
		Private currentOwner As Thread = Nothing

		''' <summary>
		''' Number of levels of locking (0 for an unowned
		''' monitor, 1 after a single call to Enter, etc).
		''' </summary>
		Private lockCount As Integer = 0

		''' <summary>
		''' Object to be used as a monitor for state changing.
		''' </summary>
		Private stateLock As New Object()

		''' <summary>
		''' AutoResetEvent used to implement Wait/Pulse/PulseAll.
		''' Initially not signalled, so that a call to Wait will
		''' block until the first pulse.
		''' </summary>
		Private waitPulseEvent As New AutoResetEvent(False)

		''' <summary>
		''' Number of threads waiting on this monitor.
		''' </summary>
		Private waitCounter As Integer = 0

		''' <summary>
		''' Event used for Enter/Exit. Initially signalled
		''' to allow the first thread to come in.
		''' </summary>
		Private enterExitEvent As New AutoResetEvent(True)

		''' <summary>
		''' Creates a new monitor, not owned by any thread.
		''' </summary>
		Public Sub New()
		End Sub

		''' <summary>
		''' Enters the monitor (locks it), blocking until the
		''' lock is held. If the monitor is already held by the current thread,
		''' its lock count is incremented.
		''' </summary>
		Public Sub Enter()
			Dim currentThread As Thread = Thread.CurrentThread
			While True
				SyncLock stateLock
					If currentOwner Is currentThread Then
						lockCount += 1
						Return
					End If
				End SyncLock

				enterExitEvent.WaitOne()
				SyncLock stateLock
					If currentOwner Is Nothing Then
						currentOwner = currentThread
						lockCount = 1
						enterExitEvent.Reset()
						Return
					End If
				End SyncLock
			End While
		End Sub

		''' <summary>
		''' Releases a level of locking, unlocking the monitor itself
		''' if the lock count becomes 0.
		''' </summary>
		''' <exception cref="SynchronizationLockException">If the current 
		''' thread does not own the monitor.</exception>
		Public Sub [Exit]()
			SyncLock stateLock
				If currentOwner IsNot Thread.CurrentThread Then
					Throw New SynchronizationLockException("Cannot Exit a monitor owned by a different thread.")
				End If
				lockCount -= 1
				If lockCount = 0 Then
					currentOwner = Nothing
					enterExitEvent.[Set]()
				End If
			End SyncLock
		End Sub

		''' <summary>
		''' Pulses the monitor once - a single waiting thread will be released
		''' and continue its execution after the current thread has exited the
		''' monitor. Unlike Pulse on the normal framework, no guarantee is
		''' made about which thread is woken.
		''' </summary>
		''' <exception cref="SynchronizationLockException">If the 
		''' current thread does not own the monitor.</exception>
		Public Sub Pulse()
			SyncLock stateLock
				If currentOwner IsNot Thread.CurrentThread Then
					Throw New SynchronizationLockException("Cannot Exit a monitor owned by a different thread.")
				End If
				' Don't bother setting the event if no-one's waiting - we'd only end
				' up having to reset the event manually.
				If waitCounter = 0 Then
					Return
				End If
				waitPulseEvent.[Set]()
				waitCounter -= 1
			End SyncLock
		End Sub

		''' <summary>
		''' Pulses the monitor such that all waiting threads are woken up.
		''' All threads will then try to regain the lock on this monitor.
		''' No order for regaining the lock is specified.
		''' </summary>
		''' <exception cref="SynchronizationLockException">If the current 
		''' thread does not own the monitor.</exception>
		Public Sub PulseAll()
			SyncLock stateLock
				If currentOwner IsNot Thread.CurrentThread Then
					Throw New SynchronizationLockException("Cannot Exit a monitor owned by a different thread.")
				End If
				For i As Integer = 0 To waitCounter - 1
					waitPulseEvent.[Set]()
				Next
				waitCounter = 0
			End SyncLock
		End Sub

		''' <summary>
		''' Relinquishes the lock on this monitor (whatever the lock count is)
		''' and waits for the monitor to be pulsed. After the monitor has been 
		''' pulsed, the thread blocks again until it has regained the lock (at 
		''' which point it will have the same lock count as it had before), and 
		''' then the method returns.
		''' </summary>
		Public Sub Wait()
			Dim oldLockCount As Integer
			SyncLock stateLock
				If currentOwner IsNot Thread.CurrentThread Then
					Throw New SynchronizationLockException("Cannot Exit a monitor owned by a different thread.")
				End If
				oldLockCount = lockCount
				' Make Exit() set the enterExitEvent
				lockCount = 1
				[Exit]()
				waitCounter += 1
			End SyncLock
			waitPulseEvent.WaitOne()
			Enter()
			' By now we own the lock again
			SyncLock stateLock
				lockCount = oldLockCount
			End SyncLock
		End Sub
	End Class

	''' <summary>
	''' Exception thrown by <see cref="Volante.Impl.CNetMonitor"/> when threading rules
	''' are violated (usually due to an operation being
	''' invoked on a monitor not owned by the current thread).
	''' </summary>
	Public Class SynchronizationLockException
		Inherits SystemException
		''' <summary>
		''' Initializes a new instance of the <see cref="SynchronizationLockException"/> class with a specified error message.
		''' </summary>
		''' <param name="message">The error message that explains the reason for the exception.</param>
		Public Sub New(message As String)
			MyBase.New(message)
		End Sub
	End Class
End Namespace
