Imports System.Collections.Generic
Imports System.Text
Namespace Volante

	Public Enum IndexType
		Unique
		NonUnique
	End Enum

	Public Enum TransactionMode
		''' <summary>
		''' Exclusive per-thread transaction: each thread accesses database in exclusive mode
		''' </summary>
		Exclusive
		''' <summary>
		''' Cooperative mode; all threads share the same transaction. Commit will commit changes made
		''' by all threads. To make this schema work correctly, it is necessary to ensure (using locking)
		''' that no thread is performing update of the database while another one tries to perform commit.
		''' Rollback will undo the work of all threads. 
		''' </summary>
		Cooperative
		''' <summary>
		''' Serializable per-thread transaction. Unlike exclusive mode, threads can concurrently access database, 
		''' but effect will be the same as them working exclusively.
		''' To provide such behavior, programmer should lock all access objects (or use hierarchical locking).
		''' When object is updated, exclusive lock should be set, otherwise shared lock is enough.
		''' Lock should be preserved until the end of transaction.
		''' </summary>
		Serializable
		#If WITH_REPLICATION Then
		''' <summary>
		''' Read only transaction which can be started at replication slave node.
		''' It runs concurrently with receiving updates from master node.
		''' </summary>
		ReplicationSlave
		#End If
	End Enum

	Public Enum CacheType
		Lru
		Strong
		Weak
	End Enum

	''' <summary> Object db
	''' </summary>
	Public Interface IDatabase
		''' <summary>Get/set database root. Database can have exactly one root. 
		''' If you need several root objects and access them by name (as is possible 
		''' in many other OODBMSes), create an index and use it as root object.
		''' Previous reference to the root object is rewritten but old root is not
		''' automatically deallocated.
		''' </summary>
		Property Root() As IPersistent

		''' <summary>Open the database
		''' </summary>
		''' <param name="filePath">path to the database file
		''' </param>
		''' <param name="cacheSizeInBytes">size of database cache, in bytes.
		''' Minimum size of the cache should be 64kB (64*1024 bytes).
		''' Larger cache usually leads to better performance. If the size is 0
		''' the cache is unlimited - and will grow to the size of the database.
		''' </param>
		Sub Open(filePath As [String], cacheSizeInBytes As Integer)

		''' <summary>Open the database with default page pool size (4 MB)
		''' </summary>
		''' <param name="filePath">path to the database file
		''' </param>
		Sub Open(filePath As [String])

		''' <summary>Open the db
		''' </summary>
		''' <param name="file">object implementing IFile interface
		''' </param>
		''' <param name="cacheSizeInBytes">size of database cache, in bytes.
		''' Minimum size of the cache should be 64kB (64*1024 bytes).
		''' Larger cache usually leads to better performance. If the size is 0
		''' the cache is unlimited - and will grow to the size of the database.
		''' </param>
		Sub Open(file As IFile, cacheSizeInBytes As Integer)

		''' <summary>Open the database with default cache size
		''' </summary>
		''' <param name="file">user specific implementation of IFile interface
		''' </param>
		Sub Open(file As IFile)

		''' <summary>Check if database is opened
		''' </summary>
		''' <returns><code>true</code>if database was opened by <code>open</code> method, 
		''' <code>false</code> otherwise
		''' </returns>        
		ReadOnly Property IsOpened() As Boolean

		''' <summary> Commit changes done by the last transaction. Transaction is started implcitly with forst update
		''' opertation.
		''' </summary>
		Sub Commit()

		''' <summary> Rollback changes made by the last transaction
		''' </summary>
		Sub Rollback()

		''' <summary>
		''' Backup current state of database
		''' </summary>
		''' <param name="stream">output stream to which backup is done</param>
		Sub Backup(stream As System.IO.Stream)

		''' <summary> Create new index. K parameter specifies key type, V - associated object type.
		''' </summary>
		''' <param name="indexType">whether index is unique (duplicate value of keys are not allowed)
		''' </param>
		''' <returns>persistent object implementing index
		''' </returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
		''' specified key type is not supported by implementation.
		''' </exception>
		Function CreateIndex(Of K, V As {Class, IPersistent})(indexType As IndexType) As IIndex(Of K, V)

		''' <summary> Create new thick index (index with large number of duplicated keys).
		''' K parameter specifies key type, V - associated object type.
		''' </summary>
		''' <returns>persistent object implementing thick index
		''' </returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_INDEX_TYPE) exception if 
		''' specified key type is not supported by implementation.
		''' </exception>
		Function CreateThickIndex(Of K, V As {Class, IPersistent})() As IIndex(Of K, V)

		''' <summary> 
		''' Create new field index
		''' K parameter specifies key type, V - associated object type.
		''' </summary>
		''' <param name="fieldName">name of the index field. Field with such name should be present in specified class <code>type</code>
		''' </param>
		''' <param name="indexType">whether index is unique (duplicate value of keys are not allowed)
		''' </param>
		''' <returns>persistent object implementing field index
		''' </returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
		''' DatabaseException(DatabaseException.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
		''' </exception>
		Function CreateFieldIndex(Of K, V As {Class, IPersistent})(fieldName As String, indexType As IndexType) As IFieldIndex(Of K, V)

		''' <summary> 
		''' Create new multi-field index
		''' </summary>
		''' <param name="fieldNames">array of names of the fields. Field with such name should be present in specified class <code>type</code>
		''' </param>
		''' <param name="indexType">whether index is unique (duplicate value of keys are not allowed)
		''' </param>
		''' <returns>persistent object implementing field index
		''' </returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.INDEXED_FIELD_NOT_FOUND) if there is no such field in specified class,
		''' DatabaseException(DatabaseException.UNSUPPORTED_INDEX_TYPE) exception if type of specified field is not supported by implementation
		''' </exception>
		Function CreateFieldIndex(Of V As {Class, IPersistent})(fieldNames As String(), indexType As IndexType) As IMultiFieldIndex(Of V)

		#If WITH_OLD_BTREE Then
		''' <summary>
		''' Create new bit index. Bit index is used to select object 
		''' with specified set of (boolean) properties.
		''' </summary>
		''' <returns>persistent object implementing bit index</returns>
		Function CreateBitIndex(Of T As {Class, IPersistent})() As IBitIndex(Of T)
		#End If

		''' <summary>
		''' Create new spatial index with integer coordinates
		''' </summary>
		''' <returns>
		''' persistent object implementing spatial index
		''' </returns>
		Function CreateSpatialIndex(Of T As {Class, IPersistent})() As ISpatialIndex(Of T)

		''' <summary>
		''' Create new R2 spatial index
		''' </summary>
		''' <returns>
		''' persistent object implementing spatial index
		''' </returns>
		Function CreateSpatialIndexR2(Of T As {Class, IPersistent})() As ISpatialIndexR2(Of T)

		''' <summary>
		''' Create new sorted collection with specified comparator
		''' </summary>
		''' <param name="comparator">comparator class specifying order in the collection</param>
		''' <param name="indexType"> whether collection is unique (members with the same key value are not allowed)</param>
		''' <returns> persistent object implementing sorted collection</returns>
		Function CreateSortedCollection(Of K, V As {Class, IPersistent})(comparator As PersistentComparator(Of K, V), indexType As IndexType) As ISortedCollection(Of K, V)

		''' <summary>
		''' Create new sorted collection. Members of this collections should implement 
		''' <code>System.IComparable</code> interface and make it possible to compare 
		''' collection members with each other as well as with serch key.
		''' </summary>
		''' <param name="indexType"> whether collection is unique (members with the same key value are not allowed)</param>
		''' <returns> persistent object implementing sorted collection</returns>
		Function CreateSortedCollection(Of K, V As {Class, IPersistent, IComparable(Of K), IComparable(Of V)})(indexType As IndexType) As ISortedCollection(Of K, V)

		''' <summary>Create set of references to persistent objects.
		''' </summary>
		''' <returns>empty set, members can be added to the set later.
		''' </returns>
		Function CreateSet(Of T As {Class, IPersistent})() As ISet(Of T)

		''' <summary>Create set of references to persistent objects.
		''' </summary>
		''' <param name="initialSize">initial size of the set</param>
		''' <returns>empty set, members can be added to the set later.
		''' </returns>
		Function CreateSet(Of T As {Class, IPersistent})(initialSize As Integer) As ISet(Of T)

		''' <summary>Create one-to-many link.
		''' </summary>
		''' <returns>empty link, members can be added to the link later.
		''' </returns>
		Function CreateLink(Of T As {Class, IPersistent})() As ILink(Of T)

		''' <summary>Create one-to-many link with specified initial size.
		''' </summary>
		''' <param name="initialSize">initial size of the array</param>
		''' <returns>empty link with specified size
		''' </returns>
		Function CreateLink(Of T As {Class, IPersistent})(initialSize As Integer) As ILink(Of T)

		''' <summary>Create dynamically extended array of referencess to persistent objects.
		''' It is intended to be used in classes using virtual properties to 
		''' access components of persistent objects.  
		''' </summary>
		''' <returns>new empty array, new members can be added to the array later.
		''' </returns>
		Function CreateArray(Of T As {Class, IPersistent})() As IPArray(Of T)

		''' <summary>Create dynamcially extended array of reference to persistent objects.
		''' It is inteded to be used in classes using virtual properties to 
		''' access components of persistent objects.  
		''' </summary>
		''' <param name="initialSize">initially allocated size of the array</param>
		''' <returns>new empty array, new members can be added to the array later.
		''' </returns>
		Function CreateArray(Of T As {Class, IPersistent})(initialSize As Integer) As IPArray(Of T)

		''' <summary> Create relation object. Unlike link which represent embedded relation and stored
		''' inside owner object, this Relation object is standalone persisitent object
		''' containing references to owner and members of the relation
		''' </summary>
		''' <param name="owner">owner of the relation
		''' </param>
		''' <returns>object representing empty relation (relation with specified owner and no members), 
		''' new members can be added to the link later.
		''' </returns>
		Function CreateRelation(Of M As {Class, IPersistent}, O As {Class, IPersistent})(owner As O) As Relation(Of M, O)

		''' <summary>
		''' Create new BLOB. Create object for storing large binary data.
		''' </summary>
		''' <returns>empty BLOB</returns>
		Function CreateBlob() As IBlob

		''' <summary>
		''' Create new time series object. 
		''' </summary>
		''' <param name="blockSize">number of elements in the block</param>
		''' <param name="maxBlockTimeInterval">maximal difference in system ticks (100 nanoseconds) between timestamps 
		''' of the first and the last elements in a block. 
		''' If value of this parameter is too small, then most blocks will contains less elements 
		''' than preallocated. 
		''' If it is too large, then searching of block will be inefficient, because index search 
		''' will select a lot of extra blocks which do not contain any element from the 
		''' specified range.
		''' Usually the value of this parameter should be set as
		''' (number of elements in block)*(tick interval)*2. 
		''' Coefficient 2 here is used to compact possible holes in time series.
		''' For example, if we collect stocks data, we will have data only for working hours.
		''' If number of element in block is 100, time series period is 1 day, then
		''' value of maxBlockTimeInterval can be set as 100*(24*60*60*10000000L)*2
		''' </param>
		''' <returns>new empty time series</returns>
		Function CreateTimeSeries(Of T As ITimeSeriesTick)(blockSize As Integer, maxBlockTimeInterval As Long) As ITimeSeries(Of T)

		#If WITH_PATRICIA Then
		''' <summary>
		''' Create PATRICIA trie (Practical Algorithm To Retrieve Information Coded In Alphanumeric)
		''' Tries are a kind of tree where each node holds a common part of one or more keys. 
		''' PATRICIA trie is one of the many existing variants of the trie, which adds path compression 
		''' by grouping common sequences of nodes together.
		''' This structure provides a very efficient way of storing values while maintaining the lookup time 
		''' for a key in O(N) in the worst case, where N is the length of the longest key. 
		''' This structure has it's main use in IP routing software, but can provide an interesting alternative 
		''' to other structures such as hashtables when memory space is of concern.
		''' </summary>
		''' <returns>created PATRICIA trie</returns>
		Function CreatePatriciaTrie(Of T As {Class, IPersistent})() As IPatriciaTrie(Of T)
		#End If

		''' <summary> Commit transaction (if needed) and close the db
		''' </summary>
		Sub Close()

		''' <summary>Explicitly start garbage collection
		''' </summary>
		''' <returns>number of collected (deallocated) objects</returns>
		Function Gc() As Integer

		#If WITH_XML Then
		''' <summary> Export database in XML format 
		''' </summary>
		''' <param name="writer">writer for generated XML document
		''' </param>
		Sub ExportXML(writer As System.IO.StreamWriter)

		''' <summary> Import data from XML file
		''' </summary>
		''' <param name="reader">XML document reader
		''' </param>
		Sub ImportXML(reader As System.IO.StreamReader)
		#End If

		''' <summary> 
		''' Retrieve object by oid. This method should be used with care because
		''' if object is deallocated, its oid can be reused. In this case
		''' GetObjectByOid() will return reference to the new object with may be
		''' different type.
		''' </summary>
		''' <param name="oid">object oid</param>
		''' <returns>reference to the object with specified oid</returns>
		Function GetObjectByOid(oid As Integer) As IPersistent

		''' <summary> 
		''' Explicitly make object peristent. Usually objects are made persistent
		''' implicitly using "persistency on reachability approach", but this
		''' method allows to do it explicitly. If object is already persistent, execution of
		''' this method has no effect.
		''' </summary>
		''' <param name="obj">object to be made persistent</param>
		''' <returns>oid assigned to the object</returns>
		Function MakePersistent(obj As IPersistent) As Integer

		#If WITH_OLD_BTREE Then
		''' Use aternative implementation of B-Tree (not using direct access to database
		''' file pages). This implementation should be used in case of serialized per thread transctions.
		''' New implementation of B-Tree will be used instead of old implementation
		''' if AlternativeBtree property is set. New B-Tree has incompatible format with 
		''' old B-Tree, so you could not use old database or XML export file with new indices. 
		''' Alternative B-Tree is needed to provide serializable transaction (old one could not be used).
		''' Also it provides better performance (about 3 times comaring with old implementation) because
		''' of object caching. And B-Tree supports keys of user defined types. 
		''' Default value: false
		Property AlternativeBtree() As Boolean
		#End If

		''' <summary>Set/get kind of object cache.
		''' If cache is CacheType.Strong none of the loaded persistent objects
		''' can be deallocated from memory by garbage collection.
		''' CacheType.Weak and CacheType.Lru both use weak references, so loaded
		''' objects can be deallocated. Lru cache can also pin some number of
		''' recently used objects for improved performance.
		''' Default value: CacheType.Lru
		''' </summary>
		Property CacheKind() As CacheType

		''' <summary>Set/get initial size of object index. Bigger values increase
		''' initial size of database but reduce number of index reallocations.
		''' Default value: 1024
		''' </summary>
		Property ObjectIndexInitSize() As Integer

		''' <summary>Set/get initial size of object cache. Default value: 1319
		''' </summary>
		Property ObjectCacheInitSize() As Integer

		''' <summary>Set/get object allocation bitmap extenstion quantum. Memory
		''' is allocated by scanning a bitmap. If there is no hole large enough,
		''' then database is extended by this value. It should not be smaller
		''' than 64 KB.
		''' Default value: 104857 bytes (1 MB)
		''' </summary>
		Property ExtensionQuantum() As Long

		''' Threshold for initiation of garbage collection. 
		''' If it is set to the value different from long.MaxValue, GC will be started each time 
		''' when delta between total size of allocated and deallocated objects exceeds specified threashold OR
		''' after reaching end of allocation bitmap in allocator.
		''' <summary>Set threshold for initiation of garbage collection. By default garbage
		''' collection is disabled (threshold is set to
		''' Int64.MaxValue). If it is set to the value different fro
		''' Long.MAX_VALUE, GC will be started each time when
		''' delta between total size of allocated and deallocated
		''' objects exceeds specified threashold OR
		''' after reaching end of allocation bitmap in allocator. 
		''' </summary>
        ''' <returns>delta between total size of allocated and deallocated object since last GC or db opening</returns>
		''' Default value: long.MaxValue
		Property GcThreshold() As Long

		''' <summary>Set/get whether garbage collection is performed in a
		''' separate thread in order to not block main application.
		''' Default value: false
		''' </summary>
		Property BackgroundGc() As Boolean

		''' <summary>Set/get whether dynamic code generation is used to generate
		''' pack/unpack methods for persisted classes.
		''' If used, serialization/deserialization of classes that only have public
		''' fields will be faster. On the downside, those methods must be generated
		''' at startup, increasing startup time.
		''' Default value: false
		''' </summary>
		Property CodeGeneration() As Boolean

		#If WITH_REPLICATION Then
		''' Request acknowledgement from slave that it receives all data before transaction
		''' commit. If this option is not set, then replication master node just writes
		''' data to the socket not warring whether it reaches slave node or not.
		''' When this option is set to true, master not will wait during each transaction commit acknowledgement
		''' from slave node. This option must be either set or not set at both
		''' slave and master nodes. If it is set only on one of this nodes then behavior of
		''' the system is unpredicted. This option can be used both in synchronous and asynchronous replication
		''' mode. The only difference is that in first case main application thread will be blocked waiting
		''' for acknowledgment, while in the asynchronous mode special replication thread will be blocked
		''' allowing thread performing commit to proceed.
		''' Default value: false
		Property ReplicationAck() As Boolean
		#End If

		''' <summary>Get database file. Should only be used to set FileListener.</summary>
		ReadOnly Property File() As IFile

		''' <summary>Get/set db listener. You can set <code>null</code> listener.
		''' </summary>
		Property Listener() As DatabaseListener

		''' <summary>
		''' Set class loader. This class loader will be used to locate classes for 
		''' loaded class descriptors. If class loader is not specified or
		''' it did find the class, then class will be searched in all active assemblies
		''' </summary>
		Property Loader() As IClassLoader

		#If CF Then
		''' <summary>
		''' Compact.NET framework doesn't allow to get list of assemblies loaded
		''' in application domain. Without it I do not know how to locate
		''' class from foreign assembly by name. 
		''' Assembly which creates Database is automatically registered.
		''' Other assemblies has to explicitely registered by programmer.
		''' </summary>
		''' <param name="assembly">registered assembly</param>
		Sub RegisterAssembly(assembly As System.Reflection.Assembly)
		#Else
		''' <summary>
		''' Create persistent class wrapper. This wrapper will implement virtual properties
		''' defined in specified class or interface, performing transparent loading and storing of persistent object
		''' </summary>
		''' <param name="type">Class or interface type of instantiated object</param>
		''' <returns>Wrapper for the specified class, implementing all virtual properties defined
		''' in it
		''' </returns>
		Function CreateClass(type As Type) As IPersistent
		#End If

		''' <summary>
		''' Begin per-thread transaction. Three types of per-thread transactions are supported: 
		''' exclusive, cooperative and serializable. In case of exclusive transaction, only one 
		''' thread can update the database. In cooperative mode, multiple transaction can work 
		''' concurrently and commit() method will be invoked only when transactions of all threads
		''' are terminated. Serializable transactions can also work concurrently. But unlike
		''' cooperative transaction, the threads are isolated from each other. Each thread
		''' has its own associated set of modified objects and committing the transaction will cause
		''' saving only of these objects to the database.To synchronize access to the objects
		''' in case of serializable transaction programmer should use lock methods
		''' of IResource interface. Shared lock should be set before read access to any object, 
		''' and exclusive lock - before write access. Locks will be automatically released when
		''' transaction is committed (so programmer should not explicitly invoke unlock method)
		''' In this case it is guaranteed that transactions are serializable.
		''' It is not possible to use <code>IPersistent.store()</code> method in
		''' serializable transactions. That is why it is also not possible to use Index and FieldIndex
		''' containers (since them are based on B-Tree and B-Tree directly access database pages
		''' and use <code>Store()</code> method to assign oid to inserted object. 
		''' You should use <code>SortedCollection</code> based on T-Tree instead or alternative
		''' B-Tree implemenataion (set AlternativeBtree property).
		''' </summary>
		''' <param name="mode"><code>TransactionMode.Exclusive</code>,  <code>TransactionMode.Cooperative</code>,
		''' <code>TransactionMode.ReplicationSlave</code> or <code>TransactionMode.Serializable</code>
		''' </param>
		Sub BeginThreadTransaction(mode As TransactionMode)

		''' <summary>
		''' End per-thread transaction started by beginThreadTransaction method.
		''' <ul>
		''' <li>If transaction is <i>exclusive</i>, this method commits the transaction and
		''' allows other thread to proceed.</li><li>
		''' If transaction is <i>serializable</i>, this method commits sll changes done by this thread
		''' and release all locks set by this thread.</li><li>     
		''' If transaction is <i>cooperative</i>, this method decrement counter of cooperative
		''' transactions and if it becomes zero - commit the work</li></ul>
		''' </summary>
		Sub EndThreadTransaction()

		''' <summary>
		''' End per-thread cooperative transaction with specified maximal delay of transaction
		''' commit. When cooperative transaction is ended, data is not immediately committed to the
		''' disk (because other cooperative transaction can be active at this moment of time).
		''' Instead of it cooperative transaction counter is decremented. Commit is performed
		''' only when this counter reaches zero value. But in case of heavy load there can be a lot of
		''' requests and so a lot of active cooperative transactions. So transaction counter never reaches zero value.
		''' If system crash happens a large amount of work will be lost in this case. 
		''' To prevent such scenario, it is possible to specify maximal delay of pending transaction commit.
		''' In this case when such timeout is expired, new cooperative transaction will be blocked until
		''' transaction is committed.
		''' </summary>
		''' <param name="maxDelay">maximal delay in milliseconds of committing transaction.  Please notice, that Volante could 
		''' not force other threads to commit their cooperative transactions when this timeout is expired. It will only
		''' block new cooperative transactions to make it possible to current transaction to complete their work.
		''' If <code>maxDelay</code> is 0, current thread will be blocked until all other cooperative trasnaction are also finished
		''' and changhes will be committed to the database.
		''' </param>
		Sub EndThreadTransaction(maxDelay As Integer)

		''' <summary>
		''' Rollback per-thread transaction. It is safe to use this method only for exclusive transactions.
		''' In case of cooperative transactions, this method rollback results of all transactions.
		''' </summary>
		Sub RollbackThreadTransaction()

		''' <summary>
		''' Get database memory dump. This function returns hashmap which key is classes
		''' of stored objects and value - TypeMemoryUsage object which specifies number of instances
		''' of particular class in the db and total size of memory used by these instance.
		''' Size of internal database structures (object index, memory allocation bitmap) is associated with 
		''' <code>IDatabase</code> class. Size of class descriptors  - with <code>System.Type</code> class.
		''' <p>This method traverse the db as garbage collection do - starting from the root object
		''' and recursively visiting all reachable objects. So it reports statistic only for visible objects.
		''' If total database size is significantly larger than total size of all instances reported
		''' by this method, it means that there is garbage in the database. You can explicitly invoke
		''' garbage collector in this case.</p> 
		''' </summary>
		Function GetMemoryUsage() As Dictionary(Of Type, TypeMemoryUsage)

		''' <summary>
		''' Get total size of all allocated objects in the database
		''' </summary>
		ReadOnly Property UsedSize() As Long

		''' <summary>
		''' Get size of the database
		''' </summary>
		ReadOnly Property DatabaseSize() As Long

		' Internal methods
		Sub deallocateObject(obj As IPersistent)

		Sub storeObject(obj As IPersistent)

		Sub storeFinalizedObject(obj As IPersistent)

		Sub loadObject(obj As IPersistent)

		Sub modifyObject(obj As IPersistent)

		Sub lockObject(obj As IPersistent)
	End Interface
End Namespace
