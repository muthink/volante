' Copyright: Krzysztof Kowalczyk
' License: BSD

Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Reflection
Imports System.Text
Imports Volante

Public Interface ITest
	Sub Run(config As TestConfig)
End Interface

Public Structure SimpleStruct
	Public v1 As Integer
	Public v2 As Long
End Structure

Public Enum RecordFullEnum
	Zero = 0
	One = 1
	Two = 2
	Three = 3
	Four = 4
	Five = 5
	Six = 6
	Seven = 7
	Eight = 8
	Nine = 9
	Ten = 10
End Enum

Public Class TestFileListener
	Inherits FileListener
	Private WriteCount As Integer = 0
	Private ReadCount As Integer = 0
	Private SyncCount As Integer = 0

	Public Overrides Sub OnWrite(pos As Long, len As Long)
		MyBase.OnWrite(pos, len)
		WriteCount += 1
	End Sub

	Public Overrides Sub OnRead(pos As Long, bufSize As Long, read As Long)
		MyBase.OnRead(pos, bufSize, read)
		ReadCount += 1
	End Sub

	Public Overrides Sub OnSync()
		MyBase.OnSync()
		SyncCount += 1
	End Sub
End Class

Public Class TestDatabaseListener
	Inherits DatabaseListener
	Private DatabaseCorruptedCount As Integer
	Private RecoveryCompletedCount As Integer
	Private GcStartedCount As Integer
	Private GcCompletedCount As Integer
	Private DeallocateObjectCount As Integer
	Private ReplicationErrorCount As Integer

	Public Overrides Sub DatabaseCorrupted()
		MyBase.DatabaseCorrupted()
		DatabaseCorruptedCount += 1
	End Sub

	Public Overrides Sub RecoveryCompleted()
		MyBase.RecoveryCompleted()
		RecoveryCompletedCount += 1
	End Sub

	Public Overrides Sub GcStarted()
		MyBase.GcStarted()
		GcStartedCount += 1
	End Sub

	Public Overrides Sub GcCompleted(nDeallocatedObjects As Integer)
		MyBase.GcCompleted(nDeallocatedObjects)
		GcCompletedCount += 1
	End Sub

	Public Overrides Sub DeallocateObject(cls As Type, oid As Integer)
		MyBase.DeallocateObject(cls, oid)
		DeallocateObjectCount += 1
	End Sub

	Public Overrides Function ReplicationError(host As String) As Boolean
		ReplicationErrorCount += 1
		Return MyBase.ReplicationError(host)
	End Function
End Class

' Note: this object should allow generating dynamic code
' for serialization/deserialization. Among other things,
' it cannot contain properties (because they are implemented
' as private backing fields), enums. The code that decides
' what can be generated like that is ClassDescriptor.generateSerializer()
Public Class RecordFull
	Inherits Persistent
	Public BoolVal As [Boolean]
	Public ByteVal As Byte
	Public SByteVal As SByte
	Public Int16Val As Int16
	Public UInt16Val As UInt16
	Public Int32Val As Int32
	Public UInt32Val As UInt32
	Public Int64Val As Int64
	Public UInt64Val As UInt64
	Public CharVal As Char
	Public FloatVal As Single
	Public DoubleVal As Double
	Public DateTimeVal As DateTime
	Public DecimalVal As [Decimal]
	Public GuidVal As Guid
	Public StrVal As String

	Public EnumVal As RecordFullEnum
	Public ObjectVal As Object

	Public Sub New()
	End Sub

	Public Overridable Sub SetValue(v As Int64)
		BoolVal = If((v Mod 2 = 0), False, True)
		ByteVal = CByte(v)
		SByteVal = CSByte(v)
		Int16Val = CType(v, Int16)
		UInt16Val = CType(v, UInt16)
		Int32Val = CType(v, Int32)
		UInt32Val = CType(v, UInt32)
		Int64Val = v
		UInt64Val = CType(v, UInt64)
		CharVal = ChrW(v)
		FloatVal = CSng(v)
		DoubleVal = Convert.ToDouble(v)
		DateTimeVal = DateTime.Now
		DecimalVal = Convert.ToDecimal(v)
		GuidVal = Guid.NewGuid()
		StrVal = v.ToString()

		Dim enumVal__1 As Integer = CInt(v Mod 11)
		EnumVal = CType(enumVal__1, RecordFullEnum)
		ObjectVal = DirectCast(v, Object)
	End Sub

	Public Sub New(v As Int64)
		SetValue(v)
	End Sub
End Class

' used for FieldIndex
Public Class RecordFullWithProperty
	Inherits RecordFull
	Public Property Int64Prop() As Int64
		Get
			Return m_Int64Prop
		End Get
		Set
			m_Int64Prop = Value
		End Set
	End Property
	Private m_Int64Prop As Int64

	Public Overrides Sub SetValue(v As Int64)
		MyBase.SetValue(v)
		Int64Prop = v
	End Sub

	Public Sub New(v As Int64)
		SetValue(v)
	End Sub

	Public Sub New()
	End Sub
End Class

Public Class TestConfig
	Const INFINITE_PAGE_POOL As Integer = 0

	Public Enum InMemoryType
		' uses a file
		No
		' uses NullFile and infinite page pool
		Full
		' uses a real file and infinite page pool. The work is done
		' in memory but on closing dta is persisted to a file
		File
	End Enum

	Public Enum FileType
		File
		' use IFile
		Stream
		' use StreamFile
	End Enum

	Public TestName As String
	Public InMemory As InMemoryType = InMemoryType.No
	Public FileKind As FileType = FileType.File
	Public AltBtree As Boolean = False
	Public Serializable As Boolean = False
	Public BackgroundGc As Boolean = False
	Public CodeGeneration As Boolean = True
	Public Encrypted As Boolean = False
	Public IsTransient As Boolean = False
	Public CacheKind As CacheType = CacheType.Lru
	Public Count As Integer = 0
	' number of iterations
	' Set by the test. Can be a subclass of TestResult
	Public Result As TestResult

	Public ReadOnly Property DatabaseName() As String
		Get
			Dim p1 As String = If(AltBtree, "_alt", "")
			Dim p2 As String = If(Serializable, "_ser", "")
			Dim p3 As String = If((InMemory = InMemoryType.Full), "_mem", "")
			Dim p4 As String = ""
			If InMemory <> InMemoryType.Full Then
				p4 = If((FileKind = FileType.File), "_file", "_stream")
			End If
			Dim p5 As String = [String].Format("_{0}", Count)
			Dim p6 As String = If(CodeGeneration, "_cg", "")
			Dim p7 As String = If(Encrypted, "_enc", "")
			Dim p8 As String = If((CacheKind <> CacheType.Lru), CacheKind.ToString(), "")
			Return [String].Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}.dbs", TestName, p1, p2, p3, p4, _
				p5, p6, p7, p8)
		End Get
	End Property

	Private Sub OpenTransientDatabase(db As IDatabase)
		Dim dbFile As New NullFile()
		dbFile.Listener = New TestFileListener()
		Tests.Assert(dbFile.NoFlush = False)
		dbFile.NoFlush = True
		Tests.Assert(dbFile.NoFlush = False)
		Tests.Assert(dbFile.Length = 0)
		db.Open(dbFile, INFINITE_PAGE_POOL)
		IsTransient = True
	End Sub

	Public Function GetDatabase(Optional delete As Boolean = True) As IDatabase
		Dim db As IDatabase = DatabaseFactory.CreateDatabase()
		Tests.Assert(db.CodeGeneration)
		Tests.Assert(Not db.BackgroundGc)
		db.Listener = New TestDatabaseListener()
		#If WITH_OLD_BTREE Then
		db.AlternativeBtree = AltBtree OrElse Serializable
		#End If
		db.BackgroundGc = BackgroundGc
		db.CacheKind = CacheKind
		db.CodeGeneration = CodeGeneration
		' TODO: make it configurable?
		' TODO: make it bigger (1000000 - the original value for h)
		If BackgroundGc Then
			db.GcThreshold = 100000
		End If
		If InMemory = InMemoryType.Full Then
			OpenTransientDatabase(db)
		Else
			Dim name = DatabaseName
			If delete Then
				Tests.TryDeleteFile(name)
			End If
			If InMemory = InMemoryType.File Then
				If FileKind = FileType.File Then
					db.Open(name, INFINITE_PAGE_POOL)
				Else
					Dim f = File.Open(name, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
					Dim sf = New StreamFile(f)
					db.Open(sf, INFINITE_PAGE_POOL)
				End If
				db.File.Listener = New TestFileListener()
			Else
				If FileKind = FileType.File Then
					If Encrypted Then
						db.Open(New Rc4File(name, "PassWord"))
					Else
						db.Open(name)
					End If
				Else
					Dim m As FileMode = FileMode.CreateNew
					If Not delete Then
						m = FileMode.OpenOrCreate
					End If
					Dim f = File.Open(name, m, FileAccess.ReadWrite, FileShare.None)
					Dim sf = New StreamFile(f)
					db.Open(sf)
				End If
				db.File.Listener = New TestFileListener()
			End If
		End If
		Return db
	End Function

	Public Sub New()
	End Sub

	Public Function Clone() As TestConfig
		Return DirectCast(MemberwiseClone(), TestConfig)
	End Function
End Class

Public Class TestResult
	Public Ok As Boolean
	Public Config As TestConfig
	Public TestName As String
	' TODO: get rid of it after converting all tests to TestConfig
	Public Count As Integer
	Public ExecutionTime As TimeSpan

	Public Overrides Function ToString() As String
		Dim name As String = TestName
		If Config IsNot Nothing Then
			name = Config.DatabaseName
		End If
		If Ok Then
			Return [String].Format("OK, {0,6} ms {1}", CInt(Math.Truncate(ExecutionTime.TotalMilliseconds)), name)
		Else
			Return [String].Format("FAILED {0}", name)
		End If
	End Function

	Public Sub Print()
		System.Console.WriteLine(ToString())
	End Sub
End Class

Public Class TestIndexNumericResult
	Inherits TestResult
	Public InsertTime As TimeSpan
End Class

Public Class Tests
	Friend Shared TotalTests As Integer = 0
	Friend Shared FailedTests As Integer = 0
	Friend Shared CurrAssertsCount As Integer = 0
	Friend Shared CurrAssertsFailed As Integer = 0

	Friend Shared FailedStackTraces As New List(Of StackTrace)()
	Private Shared Sub ResetAssertsCount()
		CurrAssertsCount = 0
		CurrAssertsFailed = 0
	End Sub

	Public Shared Function DbInstanceCount(db As IDatabase, type As Type) As Integer
		Dim mem = db.GetMemoryUsage()
		Dim mu As TypeMemoryUsage
		Dim ok As Boolean = mem.TryGetValue(type, mu)
		If Not ok Then
			Return 0
		End If
		Return mu.Count
	End Function

	Public Shared Sub DumpMemoryUsage(usages As ICollection(Of TypeMemoryUsage))
		Console.WriteLine("Memory usage")
		For Each usage As TypeMemoryUsage In usages
			Console.WriteLine((((" " + usage.Type.Name & ": instances=") + usage.Count & ", total size=") + usage.TotalSize & ", allocated size=") + usage.AllocatedSize)
		Next
	End Sub

	Public Shared Sub Assert(cond As Boolean)
		CurrAssertsCount += 1
		If cond Then
			Return
		End If
		CurrAssertsFailed += 1
		FailedStackTraces.Add(New StackTrace())
	End Sub

	Public Delegate Sub Action()
	Public Shared Sub AssertException(Of TExc As Exception)(func As Action)
		Dim gotException As Boolean = False
		Try
			func()
		Catch generatedExceptionName As TExc
			gotException = True
		End Try
		Assert(gotException)
	End Sub

	Public Shared Function ByteArraysEqual(a1 As Byte(), a2 As Byte()) As Boolean
		If a1 Is a2 Then
			Return True
		End If
		If a1 Is Nothing OrElse a2 Is Nothing Then
			Return False
		End If
		If a1.Length <> a2.Length Then
			Return False
		End If
		For i As var = 0 To a1.Length - 1
			If a1(i) <> a2(i) Then
				Return False
			End If
		Next
		Return True
	End Function

	Public Shared Sub AssertDatabaseException(func As Action, expectedCode As DatabaseException.ErrorCode)
		Dim gotException As Boolean = False
		Try
			func()
		Catch exc As DatabaseException
			gotException = exc.Code = expectedCode
		End Try
		Assert(gotException)
	End Sub

	Public Shared Function KeySeq(count As Integer) As IEnumerable(Of Long)
		Dim v As Long = 1999
		For i As Integer = 0 To count - 1
			yield Return v
			v = (3141592621L * v + 2718281829L) Mod 1000000007L
		Next
	End Function

	Public Shared Function FinalizeTest() As Boolean
		TotalTests += 1
		If CurrAssertsFailed > 0 Then
			FailedTests += 1
		End If
		Dim ok As Boolean = CurrAssertsFailed = 0
		ResetAssertsCount()
		Return ok
	End Function

	Public Shared Sub PrintFailedStackTraces()
		Dim max As Integer = 5
		For Each st As var In FailedStackTraces
			Console.WriteLine(st.ToString() & vbLf)
			If System.Threading.Interlocked.Decrement(max) = 0 Then
				Exit For
			End If
		Next
	End Sub

	Public Shared Function TryDeleteFile(path As String) As Boolean
		Try
			File.Delete(path)
			Return True
		Catch
			Return False
		End Try
	End Function

	Public Shared Sub VerifyDictionaryEnumeratorDone(de As IDictionaryEnumerator)
		AssertException(Of InvalidOperationException)(Function() 
		Console.WriteLine(de.Current)

End Function)
		AssertException(Of InvalidOperationException)(Function() 
		Console.WriteLine(de.Entry)

End Function)
		AssertException(Of InvalidOperationException)(Function() 
		Console.WriteLine(de.Key)

End Function)
		AssertException(Of InvalidOperationException)(Function() 
		Console.WriteLine(de.Value)

End Function)
		Tests.Assert(Not de.MoveNext())
	End Sub

	Public Shared Sub VerifyEnumeratorDone(e As IEnumerator)
		AssertException(Of InvalidOperationException)(Function() 
		Console.WriteLine(e.Current)

End Function)
		Tests.Assert(Not e.MoveNext())
	End Sub
End Class

Public Class TestsMain
	Const CountsIdxFast As Integer = 0
	Const CountsIdxSlow As Integer = 1
	Shared CountsIdx As Integer = CountsIdxFast
	Shared CountsDefault As Integer() = New Integer(1) {1000, 100000}
	Shared Counts1 As Integer() = New Integer(1) {200, 10000}

	Shared ConfigsDefault As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .AltBtree = True _
	}}

	' TODO: should have a separate NoFlush flag
	Shared ConfigsR2 As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .AltBtree = True _
	}}

	Shared ConfigsRaw As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .AltBtree = True _
	}}

	Shared ConfigsNoAlt As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full _
	}}

	Shared ConfigsOnlyAlt As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .AltBtree = True _
	}}

	Shared ConfigsOneFileAlt As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.File, _
		Key .AltBtree = True _
	}}

	Shared ConfigsIndex As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True, _
		Key .CacheKind = CacheType.Weak, _
		Key .Count = 2500 _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .Serializable = True _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = False _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.Full, _
		Key .AltBtree = True _
	}, _
		New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True, _
		Key .CodeGeneration = False _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True, _
		Key .Encrypted = True _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .FileKind = TestConfig.FileType.Stream, _
		Key .AltBtree = True _
	}}

	Shared ConfigsDefaultFile As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True _
	}}

	Shared ConfigsGc As TestConfig() = New TestConfig() {New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True _
	}, New TestConfig() With { _
		Key .InMemory = TestConfig.InMemoryType.No, _
		Key .AltBtree = True, _
		Key .BackgroundGc = True _
	}}

	Public Class TestInfo
		Public Name As String
		Public Configs As TestConfig()
		Public Counts As Integer()
		Public ReadOnly Property Count() As Integer
			Get
				Return Counts(CountsIdx)
			End Get
		End Property

		Public Sub New(name__1 As String, Optional configs__2 As TestConfig() = Nothing, Optional counts__3 As Integer() = Nothing)
			Name = name__1
			If configs__2 Is Nothing Then
				configs__2 = ConfigsDefault
			End If
			Configs = configs__2
			If counts__3 Is Nothing Then
				counts__3 = CountsDefault
			End If
			Counts = counts__3
		End Sub
	End Class

	' small count for TestFieldIndex and TestMultiFieldIndex because we only
	' want to test code paths unique to them. The underlying code is tested
	' in regular index tests
	#If WITH_PATRICIA Then
	#End If
	' test set below ScalableSet.BTREE_THRESHOLD, which is 128, to test
	' ILink code paths
	#If WITH_REPLICATION Then
	#End If
	#If WITH_XML Then
	#End If
	#If WITH_OLD_BTREE Then
	#End If
	' TODO: figure out why running it twice throws an exception from reflection
	' about trying to create a duplicate wrapper class
	' TODO: figure out why when it's 2000 instead of 2001 we fail
	Shared TestInfos As TestInfo() = New TestInfo() {New TestInfo("TestFieldIndex", ConfigsDefault, New Integer(1) {100, 100}), New TestInfo("TestMultiFieldIndex", ConfigsDefault, New Integer(1) {100, 100}), New TestInfo("TestR2", ConfigsR2, New Integer(1) {1500, 20000}), New TestInfo("TestRtree", ConfigsDefault, New Integer(1) {1500, 20000}), New TestInfo("TestCorrupt01", ConfigsOneFileAlt, Counts1), New TestInfo("TestIndex", ConfigsIndex, Counts1), _
		New TestInfo("TestProjection"), New TestInfo("TestL2List", ConfigsDefault, New Integer(1) {500, 500}), New TestInfo("TestTtree", ConfigsDefault, New Integer(1) {10020, 100000}), New TestInfo("TestTimeSeries", ConfigsDefault, New Integer(1) {10005, 100005}), New TestInfo("TestThickIndex"), New TestInfo("TestPatriciaTrie"), _
		New TestInfo("TestLinkPArray"), New TestInfo("TestSet", ConfigsOnlyAlt, New Integer(1) {100, 100}), New TestInfo("TestSet", ConfigsDefault, CountsDefault), New TestInfo("TestReplication", ConfigsOnlyAlt, New Integer(1) {10000, 500000}), New TestInfo("TestIndex", ConfigsIndex, Counts1), New TestInfo("TestXml", ConfigsDefaultFile, New Integer(1) {2000, 20000}), _
		New TestInfo("TestIndexRangeSearch"), New TestInfo("TestCorrupt00", ConfigsOneFileAlt), New TestInfo("TestRemove00"), New TestInfo("TestIndexUInt00"), New TestInfo("TestIndexInt00"), New TestInfo("TestIndexInt"), _
		New TestInfo("TestIndexUInt"), New TestInfo("TestIndexBoolean"), New TestInfo("TestIndexByte"), New TestInfo("TestIndexSByte"), New TestInfo("TestIndexShort"), New TestInfo("TestIndexUShort"), _
		New TestInfo("TestIndexLong"), New TestInfo("TestIndexULong"), New TestInfo("TestIndexDecimal"), New TestInfo("TestIndexFloat"), New TestInfo("TestIndexDouble"), New TestInfo("TestIndexGuid"), _
		New TestInfo("TestIndexObject"), New TestInfo("TestIndexDateTime", ConfigsDefaultFile), New TestInfo("TestIndex2"), New TestInfo("TestIndex3"), New TestInfo("TestIndex4", ConfigsDefaultFile, Counts1), New TestInfo("TestBit", ConfigsNoAlt, New Integer(1) {2000, 20000}), _
		New TestInfo("TestRaw", ConfigsRaw, New Integer(1) {1000, 10000}), New TestInfo("TestBlob", ConfigsOneFileAlt), New TestInfo("TestConcur"), New TestInfo("TestEnumerator", ConfigsDefault, New Integer(1) {50, 1000}), New TestInfo("TestList", ConfigsOnlyAlt), New TestInfo("TestBackup", ConfigsDefaultFile), _
		New TestInfo("TestGc", ConfigsGc, New Integer(1) {5000, 50000})}

	Private Shared Sub ParseCmdLineArgs(args As String())
		For Each arg As var In args
			If arg = "-fast" Then
				CountsIdx = CountsIdxFast
			ElseIf arg = "-slow" Then
				CountsIdx = CountsIdxSlow
			End If
		Next
	End Sub

	Public Shared Sub RunTests(testInfo As TestInfo)
		Dim testClassName As String = testInfo.Name
		Dim assembly__1 = Assembly.GetExecutingAssembly()
		Dim obj As Object = assembly__1.CreateInstance(testClassName)
		If obj Is Nothing Then
			obj = assembly__1.CreateInstance("Volante." & testClassName)
		End If
		Dim test As ITest = DirectCast(obj, ITest)
		For Each configTmp As TestConfig In testInfo.Configs
			#If Not WITH_OLD_BTREE Then
			Dim useAltBtree As Boolean = configTmp.AltBtree OrElse configTmp.Serializable
			If Not useAltBtree Then
				Continue For
			End If
			#End If
			Dim config = configTmp.Clone()
			If configTmp.Count <> 0 Then
				config.Count = configTmp.Count
			Else
				config.Count = testInfo.Count
			End If
			config.TestName = testClassName
			config.Result = New TestResult()
			' can be over-written by a test
			Dim start As DateTime = DateTime.Now
			test.Run(config)
			config.Result.ExecutionTime = DateTime.Now - start
			config.Result.Config = config
			' so that we Print() nicely
			config.Result.Ok = Tests.FinalizeTest()
			config.Result.Print()
		Next
	End Sub

	Public Shared Sub Main(args As String())
		ParseCmdLineArgs(args)

		Dim tStart = DateTime.Now

		For Each t As var In TestInfos
			RunTests(t)
		Next

		Dim tEnd = DateTime.Now
		Dim executionTime = tEnd - tStart

		If 0 = Tests.FailedTests Then
			Console.WriteLine([String].Format("OK! All {0} tests passed", Tests.TotalTests))
		Else
			Console.WriteLine([String].Format("FAIL! Failed {0} out of {1} tests", Tests.FailedTests, Tests.TotalTests))
		End If
		Tests.PrintFailedStackTraces()
		Console.WriteLine([String].Format("Running time: {0} ms", CInt(executionTime.TotalMilliseconds)))
	End Sub
End Class
