#If WITH_OLD_BTREE Then
Namespace Volante

	Public Class TestBitResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public SearchTime As TimeSpan
		Public RemoveTime As TimeSpan
	End Class

	Public Class TestBit
		Implements ITest
		<Flags> _
		Public Enum Options
			CLASS_A = &H1
			CLASS_B = &H2
			CLASS_C = &H4
			CLASS_D = &H8

			UNIVERAL = &H10
			SEDAN = &H20
			HATCHBACK = &H40
			MINIWAN = &H80

			AIR_COND = &H100
			CLIMANT_CONTROL = &H200
			SEAT_HEATING = &H400
			MIRROR_HEATING = &H800

			ABS = &H1000
			ESP = &H2000
			EBD = &H4000
			TC = &H8000

			FWD = &H10000
			REAR_DRIVE = &H20000
			FRONT_DRIVE = &H40000

			GPS_NAVIGATION = &H100000
			CD_RADIO = &H200000
			CASSETTE_RADIO = &H400000
			LEATHER = &H800000

			XEON_LIGHTS = &H1000000
			LOW_PROFILE_TIRES = &H2000000
			AUTOMATIC = &H4000000

			DISEL = &H10000000
			TURBO = &H20000000
			GASOLINE = &H40000000
		End Enum

		Private Class Car
			Inherits Persistent
			Friend hps As Integer
			Friend maxSpeed As Integer
			Friend timeTo100 As Integer
			Friend options As Options
			Friend model As String
			Friend vendor As String
			Friend specification As String
		End Class

		Private Class Catalogue
			Inherits Persistent
			Friend modelIndex As IFieldIndex(Of String, Car)
			Friend optionIndex As IBitIndex(Of Car)
		End Class

		Public Sub Run(config As TestConfig)
			Dim count As Integer = config.Count
			Dim res = New TestBitResult()
			config.Result = res

			Dim start As DateTime = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()

			Dim root As Catalogue = DirectCast(db.Root, Catalogue)
			Tests.Assert(root Is Nothing)
			root = New Catalogue()
			root.optionIndex = db.CreateBitIndex(Of Car)()
			root.modelIndex = db.CreateFieldIndex(Of String, Car)("model", IndexType.Unique)
			db.Root = root

			Dim rnd As Long = 1999
			Dim i As Integer, n As Integer

			Dim selectedOptions As Options = Options.TURBO Or Options.DISEL Or Options.FWD Or Options.ABS Or Options.EBD Or Options.ESP Or Options.AIR_COND Or Options.HATCHBACK Or Options.CLASS_C
			Dim unselectedOptions As Options = Options.AUTOMATIC

			i = 0
			n = 0
			While i < count
				Dim options__1 As Options = CType(rnd, Options)
				Dim car As New Car()
				car.hps = i
				car.maxSpeed = car.hps * 10
				car.timeTo100 = 12
				car.vendor = "Toyota"
				car.specification = "unknown"
				car.model = Convert.ToString(rnd)
				car.options = options__1
				root.modelIndex.Put(car)
				root.optionIndex(car) = CInt(options__1)
				If (options__1 And selectedOptions) = selectedOptions AndAlso (options__1 And unselectedOptions) = 0 Then
					n += 1
				End If
				rnd = (3141592621L * rnd + 2718281829L) Mod 1000000007L
				i += 1
			End While
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			i = 0
			For Each car As Car In root.optionIndex.[Select](CInt(selectedOptions), CInt(unselectedOptions))
				Tests.Assert((car.options And selectedOptions) = selectedOptions)
				Tests.Assert((car.options And unselectedOptions) = 0)
				i += 1
			Next
			Tests.Assert(i = n)
			res.SearchTime = DateTime.Now - start

			start = DateTime.Now
			i = 0
			For Each car As Car In root.modelIndex
				root.optionIndex.Remove(car)
				car.Deallocate()
				i += 1
			Next
			Tests.Assert(i = count)
			root.optionIndex.Clear()
			res.RemoveTime = DateTime.Now - start
			db.Close()
		End Sub
	End Class
End Namespace
#End If
