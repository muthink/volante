Imports Volante

Public Class Supplier
	Inherits Persistent
	Public name As [String]
	Public location As [String]
End Class

Public Class Detail
	Inherits Persistent
	Public id As [String]
	Public weight As Single
End Class

Public Class Shipment
	Inherits Persistent
	Public supplier As Supplier
	Public detail As Detail
	Public quantity As Integer
	Public price As Long
End Class

Public Class TestSSD
	Inherits Persistent
	Public supplierName As IFieldIndex(Of String, Supplier)
	Public detailId As IFieldIndex(Of String, Detail)
	Public shipmentSupplier As IFieldIndex(Of Supplier, Shipment)
	Public shipmentDetail As IFieldIndex(Of Detail, Shipment)

	Private Shared Sub skip(prompt As [String])
		Console.Write(prompt)
		Console.ReadLine()
	End Sub

	Private Shared Function input(prompt As System.String) As [String]
		While True
			Console.Write(prompt)
			Dim line As [String] = Console.ReadLine().Trim()
			If line.Length <> 0 Then
				Return line
			End If
		End While
	End Function

	Private Shared Function inputLong(prompt As [String]) As Long
		While True
			Try
				Return Int32.Parse(input(prompt))
			Catch generatedExceptionName As FormatException
				Console.WriteLine("Invalid integer constant")
			End Try
		End While
	End Function

	Private Shared Function inputDouble(prompt As [String]) As Double
		While True
			Try
				Return [Double].Parse(input(prompt))
			Catch generatedExceptionName As FormatException
				Console.WriteLine("Invalid floating point constant")
			End Try
		End While
	End Function

	Public Shared Sub Main(args As [String]())
		Dim db As IDatabase = DatabaseFactory.CreateDatabase()
		Dim supplier As Supplier
		Dim detail As Detail
		Dim shipment As Shipment
		Dim shipments As Shipment()
		Dim i As Integer

		db.Open("testssd.dbs")

		Dim root As TestSSD = DirectCast(db.Root, TestSSD)
		If root Is Nothing Then
			root = New TestSSD()
			root.supplierName = db.CreateFieldIndex(Of String, Supplier)("name", IndexType.Unique)
			root.detailId = db.CreateFieldIndex(Of String, Detail)("id", IndexType.Unique)
			root.shipmentSupplier = db.CreateFieldIndex(Of Supplier, Shipment)("supplier", IndexType.NonUnique)
			root.shipmentDetail = db.CreateFieldIndex(Of Detail, Shipment)("detail", IndexType.NonUnique)
			db.Root = root
		End If
		While True
			Try
				Select Case CInt(inputLong("-------------------------------------" & vbLf & "Menu:" & vbLf & "1. Add supplier" & vbLf & "2. Add detail" & vbLf & "3. Add shipment" & vbLf & "4. List of suppliers" & vbLf & "5. List of details" & vbLf & "6. Suppliers of detail" & vbLf & "7. Details shipped by supplier" & vbLf & "8. Exit" & vbLf & vbLf & ">>"))
					Case 1
						supplier = New Supplier()
						supplier.name = input("Supplier name: ")
						supplier.location = input("Supplier location: ")
						root.supplierName.Put(supplier)
						db.Commit()
						Continue Select
					Case 2
						detail = New Detail()
						detail.id = input("Detail id: ")
						detail.weight = CSng(inputDouble("Detail weight: "))
						root.detailId.Put(detail)
						db.Commit()
						Continue Select
					Case 3
						supplier = root.supplierName(input("Supplier name: "))
						If supplier Is Nothing Then
							Console.WriteLine("No such supplier!")
							Exit Select
						End If
						detail = root.detailId(input("Detail ID: "))
						If detail Is Nothing Then
							Console.WriteLine("No such detail!")
							Exit Select
						End If
						shipment = New Shipment()
						shipment.quantity = CInt(inputLong("Shipment quantity: "))
						shipment.price = inputLong("Shipment price: ")
						shipment.detail = detail
						shipment.supplier = supplier
						root.shipmentSupplier.Put(shipment)
						root.shipmentDetail.Put(shipment)
						db.Commit()
						Continue Select
					Case 4
						For Each s As Supplier In root.supplierName
							Console.WriteLine("Supplier name: " & s.name & ", supplier.location: " & s.location)
						Next
						Exit Select
					Case 5
						For Each d As Detail In root.detailId
							Console.WriteLine("Detail ID: " & d.id & ", detail.weight: " & d.weight)
						Next
						Exit Select
					Case 6
						detail = root.detailId(input("Detail ID: "))
						If detail Is Nothing Then
							Console.WriteLine("No such detail!")
							Exit Select
						End If
						shipments = DirectCast(root.shipmentDetail.[Get](New Key(detail), New Key(detail)), Shipment())
						For i = 0 To shipments.Length - 1
							Console.WriteLine("Suppplier name: " & shipments(i).supplier.name)
						Next
						Exit Select
					Case 7
						supplier = root.supplierName(input("Supplier name: "))
						If supplier Is Nothing Then
							Console.WriteLine("No such supplier!")
							Exit Select
						End If
						shipments = root.shipmentSupplier.[Get](supplier, supplier)
						For i = 0 To shipments.Length - 1
							Console.WriteLine("Detail ID: " & shipments(i).detail.id)
						Next
						Exit Select
					Case 8
						db.Close()
						Return
				End Select
				skip("Press ENTER to continue...")
			Catch x As DatabaseException
				Console.WriteLine("Error: " + x.Message)
				skip("Press ENTER to continue...")
			End Try
		End While
	End Sub
End Class

