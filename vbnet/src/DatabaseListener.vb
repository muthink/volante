
Namespace Volante
	''' <summary>
	''' Listener of database events. Programmer should derive his own subclass and register
	''' it using IDatabase.Listener property.
	''' </summary>
	Public MustInherit Class DatabaseListener
		''' <summary>
		''' Called if database was detected to be corrupted during openinig
		''' (when database was not closed properly and has to be recovered)
		''' </summary>
		Public Overridable Sub DatabaseCorrupted()
		End Sub

		''' <summary>
		''' Called after database recovery has completed
		''' </summary>
		Public Overridable Sub RecoveryCompleted()
		End Sub

		''' <summary>
		''' Called when garbage collection is started, either explicitly
		''' (by calling IDatabase.Gc()) or implicitly (after allocating
		''' enough memory to trigger gc threshold)
		''' </summary>
		Public Overridable Sub GcStarted()
		End Sub

		''' <summary>
		''' Called when garbage collection is completed
		''' </summary>
		''' <param name="nDeallocatedObjects">number of deallocated objects</param>
		'''
		Public Overridable Sub GcCompleted(nDeallocatedObjects As Integer)
		End Sub

		''' <summary>
		''' Called  when unreferenced object is deallocated from 
		''' database. It is possible to get instance of the object using
		''' <code>IDatabase.GetObjectByOid()</code> method.
		''' </summary>
		''' <param name="cls">class of deallocated object</param>
		''' <param name="oid">object identifier of deallocated object</param>
		'''
		Public Overridable Sub DeallocateObject(cls As Type, oid As Integer)
		End Sub

		''' <summary>
		''' Handle replication error 
		''' </summary>
		''' <param name="host">address of host replication to which is failed (null if error jappens at slave node)</param>
		''' <returns><code>true</code> if host should be reconnected and attempt to send data to it should be 
		''' repeated, <code>false</code> if no more attmpts to communicate with this host should be performed
		''' </returns>
		Public Overridable Function ReplicationError(host As String) As Boolean
			Return False
		End Function
	End Class
End Namespace
