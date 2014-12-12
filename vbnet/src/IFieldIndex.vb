Imports System.Collections.Generic
Imports System.Collections
Imports System.Reflection
Namespace Volante

	''' <summary> Interface of indexed field. 
	''' Index is used to provide fast access to the object by the value of indexed field. 
	''' Objects in the index are stored ordered by the value of indexed field. 
	''' It is possible to select object using exact value of the key or 
	''' select set of objects whose key belongs to a specified interval 
	''' (each boundary can be specified or unspecified and can be inclusive or exclusive)
	''' Key should be of scalar, String, DateTime or peristent object type.
	''' </summary>
	Public Interface IFieldIndex(Of K, V As {Class, IPersistent})
		Inherits IGenericIndex(Of K, V)

		''' <summary> Put new object in the index. 
		''' </summary>
		''' <param name="obj">object to be inserted in index. Object should contain indexed field. 
		''' Object can be not yet persistent, in this case its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns><code>true</code> if object is successfully inserted in the index, 
		''' <code>false</code> if index was declared as unique and there is already object with such value
		''' of the key in the index. 
		''' 
		''' </returns>
		Function Put(obj As V) As Boolean

		''' <summary>
		''' Associate new object with the key specified by object field value. 
		''' If there is already object with such key in the index, 
		''' then it will be removed from the index and new value associated with this key.
		''' </summary>
		''' <param name="obj">object to be inserted in index. Object should contain indexed field. 
		''' Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns>object previously associated with this key, <code>null</code> if there was no such object
		''' </returns>
		Function [Set](obj As V) As V

		''' <summary>
		''' Assign to the integer indexed field unique auto-icremented value and 
		''' insert object in the index. 
		''' </summary>
		''' <param name="obj">object to be inserted in index. Object should contain indexed field
		''' of integer (<code>int</code> or <code>long</code>) type.
		''' This field is assigned unique value (which will not be reused while 
		''' this index exists) and object is marked as modified.
		''' Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <exception cref="Volante.DatabaseException"><code>DatabaseException(DatabaseException.ErrorCode.INCOMPATIBLE_KEY_TYPE)</code> 
		''' is thrown when indexed field has type other than <code>int</code> or <code>long</code></exception>
		Sub Append(obj As V)

		''' <summary> Remove object with specified key from the unique index.
		''' </summary>
		''' <param name="key">wrapper of removed key
		''' </param>
		''' <returns>removed object</returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index,
		''' or DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE) if index is not unique.
		''' 
		''' </exception>
		Function Remove(key As Key) As V

		''' <summary> Remove object with specified key from the unique index.
		''' </summary>
		''' <param name="key">value of removed key
		''' </param>
		''' <returns>removed object</returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index,
		''' or DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE) if index is not unique.
		''' 
		''' </exception>
		Function RemoveKey(key As K) As V

		''' <summary>
		''' Get class object objects which can be inserted in this index
		''' </summary>
		''' <returns>class specified in IDatabase.CreateFielIndex method</returns>
		ReadOnly Property IndexedClass() As Type

		''' <summary>
		''' Get key field
		''' </summary>
		''' <returns>field info for key field</returns>
		ReadOnly Property KeyField() As MemberInfo
	End Interface

	''' <summary> Interface of multifield index. 
	''' </summary>
	Public Interface IMultiFieldIndex(Of V As {Class, IPersistent})
		Inherits IFieldIndex(Of Object(), V)
		''' <summary>
		''' Get fields used as a key
		''' </summary>
		''' <returns>array of index key fields</returns>
		ReadOnly Property KeyFields() As MemberInfo()
	End Interface
End Namespace
