Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	''' <summary> Interface of object index.
	''' Index is used to provide fast access to the object by key. 
	''' Objects in the index are stored ordered by key value. 
	''' It is possible to select object using exact value of the key or 
	''' select set of objects whose key belongs to a specified interval 
	''' (each boundary can be specified or unspecified and can be inclusive or exclusive)
	''' Key should be of scalar, String, DateTime or peristent object type.
	''' </summary>
	Public Interface IIndex(Of K, V As {Class, IPersistent})
		Inherits IGenericIndex(Of K, V)
		''' <summary>Put new object in the index. 
		''' </summary>
		''' <param name="key">object key wrapper
		''' </param>
		''' <param name="obj">object associated with this key. Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns><code>true</code> if object is successfully inserted in the index, 
		''' <code>false</code> if index was declared as unique and there is already object with such value
		''' of the key in the index. 
		''' </returns>
		Function Put(key As Key, obj As V) As Boolean

		''' <summary>Put new object in the index. 
		''' </summary>
		''' <param name="key">object key value
		''' </param>
		''' <param name="obj">object associated with this key. Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns><code>true</code> if object is successfully inserted in the index, 
		''' <code>false</code> if index was declared as unique and there is already object with such value
		''' of the key in the index. 
		''' 
		''' </returns>
		Function Put(key As K, obj As V) As Boolean

		''' <summary>Associate new value with the key. If there is already object with such key in the index, 
		''' then it will be removed from the index and new value associated with this key.
		''' </summary>
		''' <param name="key">object key wrapper
		''' </param>
		''' <param name="obj">object associated with this key. Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns>object previously associated with this key, <code>null</code> if there was no such object
		''' </returns>
		Function [Set](key As Key, obj As V) As V

		''' <summary>Associate new value with the key. If there is already object with such key in the index, 
		''' then it will be removed from the index and new value associated with this key.
		''' </summary>
		''' <param name="key">object key value
		''' </param>
		''' <param name="obj">object associated with this key. Object can be not yet peristent, in this case
		''' its forced to become persistent by assigning oid to it.
		''' </param>
		''' <returns>object previously associated with this key, <code>null</code> if there was no such object
		''' </returns>
		Function [Set](key As K, obj As V) As V

		''' <summary>Remove object with specified key from the tree.
		''' </summary>
		''' <param name="key">wrapper of the value of the key of removed object
		''' </param>
		''' <param name="obj">object removed from the index
		''' </param>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index
		''' 
		''' </exception>
        Overloads Sub Remove(key As Key, obj As V)

		''' <summary>Remove object with specified key from the tree.
		''' </summary>
		''' <param name="key">value of the key of removed object
		''' </param>
		''' <param name="obj">object removed from the index
		''' </param>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index
		''' 
		''' </exception>
        Overloads Sub Remove(key As K, obj As V)

		''' <summary>Remove key from the unique index.
		''' </summary>
		''' <param name="key">wrapper of removed key
		''' </param>
		''' <returns>removed object</returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index,
		''' or DatabaseException(DatabaseException.ErrorCode.KEY_NOT_UNIQUE) if index is not unique.
		''' 
		''' </exception>
        Overloads Function Remove(key As Key) As V

		''' <summary>Remove key from the unique index.
		''' </summary>
		''' <param name="key">value of removed key
		''' </param>
		''' <returns>removed object</returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND) exception if there is no such key in the index,
		''' or DatabaseException(DatabaseException.ErrorCode.KEY_NOV_UNIQUE) if index is not unique.
		''' 
		''' </exception>
		Function RemoveKey(key As K) As V
	End Interface
End Namespace
