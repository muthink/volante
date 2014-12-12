
Namespace Volante

	''' <summary>Database factory
	''' </summary>
	Public Class DatabaseFactory
		''' <summary>Create a database instance
		''' </summary>
		Public Shared Function CreateDatabase() As IDatabase
			#If CF Then
			Return New DatabaseImpl(System.Reflection.Assembly.GetCallingAssembly())
			#Else
			Return New DatabaseImpl()
			#End If
		End Function

		#If Not CF AndAlso WITH_REPLICATION Then
		''' <summary>
		''' Create new instance of the master node of replicated database
		''' </summary>
		''' <param name="replicationSlaveNodes">addresses of hosts to which replication will be performed. 
		''' Address as specified as NAME:PORT</param>
		''' <param name="asyncBufSize">if value of this parameter is greater than zero then replication will be 
		''' asynchronous, done by separate thread and not blocking main application. 
		''' Otherwise data is send to the slave nodes by the same thread which updates the database.
		''' If space asynchronous buffer is exhausted, then main thread willbe also blocked until the
		''' data is send.</param>
		''' <returns>new instance of the master database (unopened, you should explicitely invoke open method)</returns>
		'''
		Public Shared Function CreateReplicationMasterDatabase(replicationSlaveNodes As String(), asyncBufSize As Integer) As ReplicationMasterDatabase
			Return New ReplicationMasterDatabaseImpl(replicationSlaveNodes, asyncBufSize)
		End Function

		''' <summary>
		''' Create new instance of the slave node of replicated database
		''' </summary>
		''' <param name="port">socket port at which connection from master will be established</param>
		''' <returns>new instance of the slave db (unopened, you should explicitely invoke open method)</returns>
		'''/
		Public Shared Function CreateReplicationSlaveDatabase(port As Integer) As ReplicationSlaveDatabase
			Return New ReplicationSlaveDatabaseImpl(port)
		End Function
		#End If
	End Class
End Namespace
