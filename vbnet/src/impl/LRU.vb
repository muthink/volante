Imports Volante
Namespace Volante.Impl

	Class LRU
		Friend [next] As LRU
		Friend prev As LRU

		Friend Sub New()
			[next] = InlineAssignHelper(prev, Me)
		End Sub

		Friend Sub unlink()
			[next].prev = prev
			prev.[next] = [next]
		End Sub

		Friend Sub link(node As LRU)
			node.[next] = [next]
			node.prev = Me
			[next].prev = node
			[next] = node
		End Sub
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
