Imports System.IO
Imports System.Runtime.InteropServices
Imports Volante.Impl
Namespace Volante

	Public Class OsFile
		Implements IFile
		Public Property Listener() As FileListener
			Get
				Return m_Listener
			End Get
			Set
				m_Listener = Value
			End Set
		End Property
		Private m_Listener As FileListener

		#If Not MONO AndAlso Not CF AndAlso Not SILVERLIGHT Then
		#If NET_4_0 Then
		#End If
		<System.Security.SecuritySafeCritical> _
		<DllImport("kernel32.dll", SetLastError := True)> _
		Private Shared Function FlushFileBuffers(fileHandle As Microsoft.Win32.SafeHandles.SafeFileHandle) As Integer
		End Function
		#End If
		Public Overridable Sub Write(pos As Long, buf As Byte())
			file.Seek(pos, SeekOrigin.Begin)
			file.Write(buf, 0, buf.Length)
			If Listener IsNot Nothing Then
				Listener.OnWrite(pos, buf.Length)
			End If
		End Sub

		Public Overridable Function Read(pos As Long, buf As Byte()) As Integer
			file.Seek(pos, SeekOrigin.Begin)
			Dim len As Integer = file.Read(buf, 0, buf.Length)
			If Listener IsNot Nothing Then
				Listener.OnRead(pos, buf.Length, len)
			End If
			Return len
		End Function

		#If NET_4_0 Then
		#End If
		<System.Security.SecuritySafeCritical> _
		Public Overridable Sub Sync()
			file.Flush()
			#If Not CF AndAlso Not MONO AndAlso Not SILVERLIGHT Then
			If Not NoFlush Then
				FlushFileBuffers(file.SafeFileHandle)
			End If
			#End If
			If Listener IsNot Nothing Then
				Listener.OnSync()
			End If
		End Sub

		''' Whether to not flush file buffers during transaction commit. It will increase performance because
		''' it eliminates synchronous write to the disk. It can cause database corruption in case of 
		''' OS or power failure. Abnormal termination of application itself should not cause
		''' the problem, because all data written to a file but not yet saved to the disk is 
		''' stored in OS file buffers andwill be written to the disk.
		''' Default value: false
		Public Property NoFlush() As Boolean
			Get
				Return m_NoFlush
			End Get
			Set
				m_NoFlush = Value
			End Set
		End Property
		Private m_NoFlush As Boolean

		Public Overridable Sub Close()
			file.Close()
		End Sub

		Public Overridable Sub Lock()
			#If Not CF Then
			file.Lock(0, Long.MaxValue)
			#End If
		End Sub

		Public ReadOnly Property Length() As Long
			Get
				Return file.Length
			End Get
		End Property


		Public Sub New(filePath As [String])
			Me.New(filePath, False)
		End Sub

		Public Sub New(filePath As [String], [readOnly] As Boolean)
			NoFlush = False
			file = New FileStream(filePath, FileMode.OpenOrCreate, If([readOnly], FileAccess.Read, FileAccess.ReadWrite))
		End Sub

		Protected file As FileStream
	End Class
End Namespace
