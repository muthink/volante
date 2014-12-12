
Namespace Volante
	''' <summary>
	''' Information about memory usage for one type. 
	''' Instances of this class are created by IDatabase.GetMemoryUsage method.
	''' Size of internal database structures (object index,* memory allocation bitmap) is associated with 
	''' <code>Database</code> class. Size of class descriptors  - with <code>System.Type</code> class.
	''' </summary>
	Public Class TypeMemoryUsage
		''' <summary>
		''' Class of persistent object or Database for database internal data
		''' </summary>
		Public Type As Type

		''' <summary>
		''' Number of reachable instance of the particular class in the database.
		''' </summary>
		Public Count As Integer

		''' <summary>
		''' Total size of all reachable instances
		''' </summary>
		Public TotalSize As Long

		''' <summary>
		''' Real allocated size of all instances. Database allocates space for th objects using quantums,
		''' for example object wilth size 25 bytes will use 32 bytes in the db.
		''' In item associated with Database class this field contains size of all allocated
		''' space in the database (marked as used in bitmap) 
		''' </summary>
		Public AllocatedSize As Long

		''' <summary>
		''' TypeMemoryUsage constructor
		''' </summary>
		Public Sub New(type As Type)
			Me.Type = type
		End Sub
	End Class
End Namespace
