Namespace Volante

	''' <summary>
	''' Common interface for all PArrays
	''' </summary> 
	Public Interface IGenericPArray
		''' <summary> Get number of the array elements
		''' </summary>
		''' <returns>the number of related objects
		''' 
		''' </returns>
		Function Size() As Integer

		''' <summary>Get oid of array element.
		''' </summary>
		''' <param name="i">index of the object in the relation
		''' </param>
		''' <returns>oid of the object (0 if array contains <code>null</code> reference)
		''' </returns>
		Function GetOid(i As Integer) As Integer

		''' <summary>
		''' Set owner object for this PArray. Owner is persistent object contaning this PArray.
		''' This method is mostly used by db itself, but can also used explicitly by programmer if
		''' Parray component of one persistent object is assigned to component of another persistent object
		''' </summary>
		''' <param name="owner">owner of the array</param>
		Sub SetOwner(owner As IPersistent)
	End Interface

	''' <summary>Dynamically extended array of references to persistent objects.
	''' It is inteded to be used in classes using virtual properties to 
	''' access components of persistent objects. You can not use standard
	''' C# array here, instead you should use PArray class.
	''' PArray is created by IDatabase.CreateArray method
	''' </summary>
	Public Interface IPArray(Of T As {Class, IPersistent})
		Inherits IGenericPArray
		Inherits ILink(Of T)
	End Interface
End Namespace
