Imports Volante
Imports System.Collections

Public Class Supplier
	Inherits Persistent
	Public name As [String]
	Public location As [String]
	Public orders As Relation(Of Order, Supplier)
End Class

Public Class Detail
	Inherits Persistent
	Public id As [String]
	Public weight As Single
	Public orders As Relation(Of Order, Detail)
End Class

Public Class Order
	Inherits Persistent
	Public supplier As Relation(Of Order, Supplier)
	Public detail As Relation(Of Order, Detail)
	Public quantity As Integer
	Public price As Long

	Public Class QuantityComparer
		Implements IComparer
		Public Function Compare(a As Object, b As Object) As Integer Implements IComparer.Compare
			Return DirectCast(a, Order).quantity - DirectCast(b, Order).quantity
		End Function
	End Class
	Public Shared quantityComparer As New QuantityComparer()
End Class

Public Class TestSOD
	Inherits Persistent
	Public supplierName As IFieldIndex(Of String, Supplier)
	Public detailId As IFieldIndex(Of String, Detail)

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
		Dim order__1 As Order
		Dim orders As Order()
		Dim d2o As New Projection(Of Detail, Order)("orders")
		Dim s2o As New Projection(Of Supplier, Order)("orders")
		Dim i As Integer

		db.Open("testsod.dbs")

		Dim root As TestSOD = DirectCast(db.Root, TestSOD)
		If root Is Nothing Then
			root = New TestSOD()
			root.supplierName = db.CreateFieldIndex(Of String, Supplier)("name", IndexType.Unique)
			root.detailId = db.CreateFieldIndex(Of String, Detail)("id", IndexType.Unique)
			db.Root = root
		End If
		While True
			Try
				Select Case CInt(inputLong("-------------------------------------" & vbLf & "Menu:" & vbLf & "1. Add supplier" & vbLf & "2. Add detail" & vbLf & "3. Add Order" & vbLf & "4. List of suppliers" & vbLf & "5. List of details" & vbLf & "6. Suppliers of detail" & vbLf & "7. Details shipped by supplier" & vbLf & "8. Orders for detail of supplier" & vbLf & "9. Exit" & vbLf & vbLf & ">>"))
					Case 1
						supplier = New Supplier()
						supplier.name = input("Supplier name: ")
						supplier.location = input("Supplier location: ")
						supplier.orders = db.CreateRelation(Of Order, Supplier)(supplier)
						root.supplierName.Put(supplier)
						db.Commit()
						Continue Select
					Case 2
						detail = New Detail()
						detail.id = input("Detail id: ")
						detail.weight = CSng(inputDouble("Detail weight: "))
						detail.orders = db.CreateRelation(Of Order, Detail)(detail)
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
						order__1 = New Order()
						order__1.quantity = CInt(inputLong("Order quantity: "))
						order__1.price = inputLong("Order price: ")
						order__1.detail = detail.orders
						order__1.supplier = supplier.orders
						detail.orders.Add(order__1)
						supplier.orders.Add(order__1)
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
						detail = DirectCast(root.detailId(input("Detail ID: ")), Detail)
						If detail Is Nothing Then
							Console.WriteLine("No such detail!")
							Exit Select
						End If
						For Each o As Order In detail.orders
							supplier = DirectCast(o.supplier.Owner, Supplier)
							Console.WriteLine("Suppplier name: " & supplier.name)
						Next
						Exit Select
					Case 7
						supplier = root.supplierName(input("Supplier name: "))
						If supplier Is Nothing Then
							Console.WriteLine("No such supplier!")
							Exit Select
						End If
						For Each o As Order In supplier.orders
							detail = DirectCast(o.detail.Owner, Detail)
							Console.WriteLine("Detail ID: " & detail.id)
						Next
						Exit Select
					Case 8
						d2o.Reset()
						d2o.Project(root.detailId.StartsWith(input("Detail ID prefix: ")))
						s2o.Reset()
						s2o.Project(root.supplierName.StartsWith(input("Supplier name prefix: ")))
						s2o.Join(d2o)
						orders = s2o.ToArray()
						Array.Sort(orders, 0, orders.Length, Order.quantityComparer)
						For i = 0 To orders.Length - 1
							order__1 = orders(i)
							supplier = order__1.supplier.Owner
							detail = order__1.detail.Owner
							Console.WriteLine("Detail ID: " & detail.id & ", supplier name: " & supplier.name & ", quantity: " & order__1.quantity)
						Next
						Exit Select
					Case 9
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

