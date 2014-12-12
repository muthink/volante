Namespace Volante
	''' <summary>
	''' Database performing replication of changed pages to specified slave nodes.
	''' </summary>
	Public Interface ReplicationMasterDatabase
		Inherits IDatabase
		''' <summary>
		''' Get number of currently available slave nodes
		''' </summary>
		''' <returns>number of online replication slaves</returns>
		Function GetNumberOfAvailableHosts() As Integer
	End Interface
End Namespace
