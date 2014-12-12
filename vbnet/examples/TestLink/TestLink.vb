Imports Volante

Class Detail
	Inherits Persistent
	Friend name As [String]
	Friend color As [String]
	Friend weight As Double
	Friend orders As ILink(Of Order)
End Class

Class Supplier
	Inherits Persistent
	Friend name As [String]
	Friend address As [String]
	Friend orders As ILink(Of Order)
End Class

Class Order
	Inherits Persistent
	Friend detail As Detail
	Friend supplier As Supplier
	Friend quantity As Integer
	Friend price As Long
End Class

Class Root
	Inherits Persistent
	Friend details As IFieldIndex(Of String, Detail)
	Friend suppliers As IFieldIndex(Of String, Supplier)
End Class

Public Class TestLink
	Friend Shared Function input(prompt As System.String) As System.String
		While True
			Console.Write(prompt)
			Dim line As [String] = Console.ReadLine().Trim()
			If line.Length <> 0 Then
				Return line
			End If
		End While
	End Function

	Private Shared Function inputInt(prompt As [String]) As Integer
		While True
			Try
				Return Int32.Parse(input(prompt))
			Catch generatedExceptionName As Exception
			End Try
		End While
	End Function

	Private Shared Function inputReal(prompt As [String]) As Double
		While True
			Try
				Return [Double].Parse(input(prompt))
			Catch generatedExceptionName As Exception
			End Try
		End While
	End Function

	Public Shared Sub Main(args As [String]())
		Dim name As [String]
		Dim supplier As Supplier
		Dim detail As Detail
		Dim suppliers As Supplier()
		Dim details As Detail()
		Dim order As Order
		Dim db As IDatabase = DatabaseFactory.CreateDatabase()
		db.Open("testlist.dbs")
		Dim root As Root = DirectCast(db.Root, Root)

		If root Is Nothing Then
			root = New Root()
			root.details = db.CreateFieldIndex(Of String, Detail)("name", IndexType.Unique)
			root.suppliers = db.CreateFieldIndex(Of String, Supplier)("name", IndexType.Unique)
			db.Root = root
		End If
		While True
			Console.WriteLine("------------------------------------------")
			Console.WriteLine("1. Add supplier")
			Console.WriteLine("2. Add detail")
			Console.WriteLine("3. Add order")
			Console.WriteLine("4. Search suppliers")
			Console.WriteLine("5. Search details")
			Console.WriteLine("6. Suppliers of detail")
			Console.WriteLine("7. Deails shipped by supplier")
			Console.WriteLine("8. Exit")
			Dim str As [String] = input("> ")
			Dim cmd As Integer
			Try
				cmd = Int32.Parse(str)
			Catch generatedExceptionName As Exception
				Console.WriteLine("Invalid command")
				Continue Try
			End Try
			Select Case cmd
				Case 1
					supplier = New Supplier()
					supplier.name = input("Supplier name: ")
					supplier.address = input("Supplier address: ")
					supplier.orders = db.CreateLink(Of Order)()
					root.suppliers.Put(supplier)
					Exit Select
				Case 2
					detail = New Detail()
					detail.name = input("Detail name: ")
					detail.weight = inputReal("Detail weight: ")
					detail.color = input("Detail color: ")
					detail.orders = db.CreateLink(Of Order)()
					root.details.Put(detail)
					Exit Select
				Case 3
					order = New Order()
					name = input("Supplier name: ")
					order.supplier = root.suppliers(name)
					If order.supplier Is Nothing Then
						Console.WriteLine("No such supplier")
						Continue Select
					End If
					name = input("Detail name: ")
					order.detail = root.details(name)
					If order.detail Is Nothing Then
						Console.WriteLine("No such detail")
						Continue Select
					End If
					order.quantity = inputInt("Quantity: ")
					order.price = inputInt("Price: ")
					order.detail.orders.Add(order)
					order.supplier.orders.Add(order)
					order.detail.Store()
					order.supplier.Store()
					Exit Select
				Case 4
					name = input("Supplier name prefix: ")
					suppliers = root.suppliers.[Get](New Key(name), New Key(name & ChrW(255), False))
					If suppliers.Length = 0 Then
						Console.WriteLine("No such suppliers found")
					Else
						For i As Integer = 0 To suppliers.Length - 1
							Console.WriteLine(suppliers(i).name & ControlChars.Tab & suppliers(i).address)
						Next
					End If
					Continue Select
				Case 5
					name = input("Detail name prefix: ")
					details = root.details.[Get](New Key(name), New Key(name & ChrW(255), False))
					If details.Length = 0 Then
						Console.WriteLine("No such details found")
					Else
						For i As Integer = 0 To details.Length - 1
							Console.WriteLine(details(i).name & ControlChars.Tab & details(i).weight & ControlChars.Tab & details(i).color)
						Next
					End If
					Continue Select
				Case 6
					name = input("Detail name: ")
					detail = DirectCast(root.details(name), Detail)
					If detail Is Nothing Then
						Console.WriteLine("No such detail")
					Else
						Dim i As Integer = detail.orders.Length
						While System.Threading.Interlocked.Decrement(i) >= 0
							Console.WriteLine(DirectCast(detail.orders(i), Order).supplier.name)
						End While
					End If
					Continue Select
				Case 7
					name = input("Supplier name: ")
					supplier = DirectCast(root.suppliers(name), Supplier)
					If supplier Is Nothing Then
						Console.WriteLine("No such supplier")
					Else
						Dim i As Integer = supplier.orders.Length
						While System.Threading.Interlocked.Decrement(i) >= 0
							Console.WriteLine(DirectCast(supplier.orders(i), Order).detail.name)
						End While
					End If
					Continue Select
				Case 8
					db.Close()
					Console.WriteLine("End of session")
					Return
				Case Else
					Console.WriteLine("Invalid command")
					Continue Select
			End Select
			db.Commit()
		End While
	End Sub
End Class
