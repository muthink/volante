Namespace Volante
	''' <summary>
	''' Database reciving modified pages from replication master and 
	''' been able to run read-only transactions 
	''' </summary>
	Public Interface ReplicationSlaveDatabase
		Inherits IDatabase
		''' <summary>
		''' Check if socket is connected to the master host
		''' </summary>
		''' <returns><code>true</code> if connection between slave and master is sucessfully established</returns>
		Function IsConnected() As Boolean

		''' <summary>
		''' Wait until database is modified by master
		''' This method blocks current thread until master node commits trasanction and
		''' this transanction is completely delivered to this slave node
		''' </summary>
		Sub WaitForModification()
	End Interface
End Namespace
