#If WITH_REPLICATION Then
Imports Volante
Namespace Volante.Impl

	Public Class ReplicationMasterDatabaseImpl
		Inherits DatabaseImpl
		Inherits ReplicationMasterDatabase
		Public Sub New(hosts As String(), asyncBufSize As Integer)
			Me.hosts = hosts
			Me.asyncBufSize = asyncBufSize
		End Sub

		Public Overrides Sub Open(file As IFile, cacheSizeInBytes As Integer)
			MyBase.Open(If(asyncBufSize <> 0, DirectCast(New AsyncReplicationMasterFile(Me, file, asyncBufSize), ReplicationMasterFile), New ReplicationMasterFile(Me, file)), cacheSizeInBytes)
		End Sub

		Public Function GetNumberOfAvailableHosts() As Integer
			Return DirectCast(pool.file, ReplicationMasterFile).GetNumberOfAvailableHosts()
		End Function

		Friend hosts As String()
		Friend asyncBufSize As Integer
	End Class
End Namespace
#End If
