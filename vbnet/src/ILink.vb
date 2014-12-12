Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	''' <summary>
	''' Common interface for all links
	''' </summary>
	Public Interface IGenericLink
		''' <summary>Get number of the linked objects 
		''' </summary>
		''' <returns>the number of related objects
		''' 
		''' </returns>
		Function Size() As Integer

		''' <summary>Get related object by index without loading it.
		''' Returned object can be used only to get its oid or to compare with other objects using
		''' <code>equals</code> method
		''' </summary>
		''' <param name="i">index of the object in the relation
		''' </param>
		''' <returns>stub representing referenced object
		''' 
		''' </returns>
		Function GetRaw(i As Integer) As IPersistent

		''' <summary>
		''' Set owner object for this link. Owner is persistent object contaning this link.
		''' This method is mostly used by db itself, but can also used explicitly by programmer if
		''' link component of one persistent object is assigned to component of another persistent object
		''' </summary>
		''' <param name="owner">link owner</param>
		Sub SetOwner(owner As IPersistent)

		''' <summary>
		''' Replace all direct references to linked objects with stubs. 
		''' This method is needed to avoid memory exhaustion in case when 
		''' there is a large number of objects in database, mutually
		''' refefencing each other (each object can directly or indirectly 
		''' be accessed from other objects).
		''' </summary>
		Sub Unpin()

		''' <summary>Replace references to elements with direct references.
		''' It will impove spped of manipulations with links, but it can cause
		''' recursive loading in memory large number of objects and as a result - memory
		''' overflow, because garabge collector will not be able to collect them
		''' </summary>
		Sub Pin()
	End Interface

	''' <summary>Interface for one-to-many relation. There are two types of relations:
	''' embedded (when references to the related objects are stored in relation
	''' owner object itself) and standalone (when relation is a separate object, which contains
	''' the reference to the relation owner and relation members). Both kinds of relations
	''' implements ILink interface. Embedded relation is created by IDatabase.CreateLink() method
	''' and standalone relation is represented by Relation persistent class created by
	''' IDatabase.CreateRelation() method.
	''' </summary>
	Public Interface ILink(Of T As {Class, IPersistent})
		Inherits IList(Of T)
		Inherits IGenericLink
		''' <summary>Number of the linked objects 
		''' </summary>
		Property Length() As Integer

		''' <summary>Get related object by index
		''' </summary>
		''' <param name="i">index of the object in the relation
		''' </param>
		''' <returns>referenced object
		''' 
		''' </returns>
		Function [Get](i As Integer) As T

		''' <summary>Replace i-th element of the relation
		''' </summary>
		''' <param name="i">index in the relartion
		''' </param>
		''' <param name="obj">object to be included in the relation     
		''' 
		''' </param>
		Sub [Set](i As Integer, obj As T)

		''' <summary>Add all elements of the array to the relation
		''' </summary>
		''' <param name="arr">array of objects which should be added to the relation
		''' 
		''' </param>
		Sub AddAll(arr As T())

		''' <summary>Add specified elements of the array to the relation
		''' </summary>
		''' <param name="arr">array of objects which should be added to the relation
		''' </param>
		''' <param name="from">index of the first element in the array to be added to the relation
		''' </param>
		''' <param name="length">number of elements in the array to be added in the relation
		''' </param>
		Sub AddAll(arr As T(), from As Integer, length As Integer)

		''' <summary>Add all object members of the other relation to this relation
		''' </summary>
		''' <param name="link">another relation
		''' 
		''' </param>
		Sub AddAll(link As ILink(Of T))

		''' <summary>Get relation members as array of objects
		''' </summary>
		''' <returns>created array</returns>
		Function ToArray() As T()

		''' <summary>Return array with relation members. Members are not loaded and 
		''' size of the array can be greater than actual number of members. 
		''' </summary>
		''' <returns>array of object with relation members used in implementation of Link class
		''' </returns>
		Function ToRawArray() As Array

		''' <summary>Check if i-th element of Link is the same as specified obj
		''' </summary>
		''' <param name="i"> element index</param>
		''' <param name="obj">specified object</param>
		''' <returns><code>true</code> if i-th element of Link reference the same object as "obj"</returns>
		Function ContainsElement(i As Integer, obj As T) As Boolean
	End Interface
End Namespace
