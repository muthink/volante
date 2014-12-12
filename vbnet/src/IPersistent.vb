Namespace Volante

	''' <summary>Interface for persisted objects
	''' </summary>
	Public Interface IPersistent
		''' <summary>Get object identifier
		''' </summary>
		ReadOnly Property Oid() As Integer

		''' <summary> Get db in which this object is stored
		''' </summary>
		ReadOnly Property Database() As IDatabase

		''' <summary>Load object from the database (if needed)
		''' </summary>
		Sub Load()

		''' 
		''' <summary>Check if object is stub and has to be loaded from the database
		''' </summary>
		''' <returns><code>true</code> if object has to be loaded from the database
		''' </returns>
		Function IsRaw() As Boolean

		''' <summary>Check if object is persistent 
		''' </summary>
		''' <returns><code>true</code> if object has assigned oid
		''' 
		''' </returns>
		Function IsPersistent() As Boolean

		''' <summary>Check if object is deleted by garbage collection
		''' </summary>
		''' <returns> <code>true</code> if object is deleted by GC
		''' </returns>
		Function IsDeleted() As Boolean

		''' <summary>Check if object was modified within current transaction
		''' </summary>
		''' <returns><code>true</code> if object is persistent and was modified within current transaction
		''' 
		''' </returns>
		Function IsModified() As Boolean

		''' <summary>Usually objects are made persistent
		''' implicitly using "persistency on reachability" approach. This
		''' method allows you to do it explicitly 
		''' </summary>
		''' <param name="db">db in which object should be stored 
		''' </param>
		''' <returns>oid assigned to the object</returns>
		Function MakePersistent(db As IDatabase) As Integer

		''' <summary>Save object in the database
		''' </summary>
		Sub Store()

		''' <summary>
		''' Mark object as modified. Object will be saved to the database during transaction commit
		''' </summary>
		Sub Modify()

		''' <summary>Deallocate persistent object from the database
		''' </summary>
		Sub Deallocate()

		''' <summary>Specified whether object should be automatically loaded when it is referenced
		''' by other loaded peristent object. Default implementation of this method
		''' returns <code>true</code> making all cluster of referenced objects loaded together. 
		''' To avoid main memory overflow you should stop recursive loading of all objects
		''' from the database to main memory by redefining this method in some classes and returning
		''' <code>false</code> in it. In this case object has to be loaded explicitely 
		''' using Persistent.load method.
		''' </summary>
		''' <returns><code>true</code> if object is automatically loaded
		''' 
		''' </returns>
		Function RecursiveLoading() As Boolean

		''' <summary>Called by the database after loading the object.
		''' It can be used to initialize transient fields of the object. 
		''' Default implementation of this method does nothing 
		''' </summary>
		Sub OnLoad()

		''' <summary>Called by the database before storing the object.
		''' Default implementation of this method does nothing 
		''' </summary>
		Sub OnStore()

		''' <summary>
		''' Invalidate object. Invalidated object has to be explicitly
		''' reloaded using Load() method. Attempt to store invalidated object
		''' will cause DatabaseException exception.
		''' </summary>
		Sub Invalidate()

		''' <summary>
		''' Associate object with db.
		''' This method is used by IDictionary class and you should not use it explicitly.
		''' </summary>
		''' <param name="db">database to be assigned to</param>
		''' <param name="oid">assigned oid</param>
		''' <param name="raw">if object is already loaded</param>
		Sub AssignOid(db As IDatabase, oid As Integer, raw As Boolean)
	End Interface
End Namespace
