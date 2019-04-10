using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.Extensions.Logging;
using RF.Contracts.Models.Keys;
using RF.Domain.MarketCache.Contracts.Enumerations;
using RF.Domain.MarketCache.Contracts.Interfaces.Data;
using RF.Domain.MarketCache.Contracts.Interfaces.Pipelines;
using RF.Domain.MarketCache.Contracts.Models.Data.Base;
using RF.Domain.MarketCache.Contracts.Models.TreeCommands;

namespace RF.Domain.MarketCache.Trees.Nodes.Base
{
    public class DictionaryNode : Node
    {
        protected internal string childArrayName;
        protected internal ImmutableDictionary<long, Node> children = ImmutableDictionary<long, Node>.Empty;

        public DictionaryNode(ProviderKey provider, TreeNode root, Node parentNode, NodeData value, ENodeType nodeType, string childArrayName, INodePipeline pipeline = null)
            : base(provider, root, parentNode, value, nodeType, pipeline)
        {
            this.childArrayName = childArrayName;
        }
        
        public override int Count => children.Count;

        public override IEnumerator<INode> GetEnumerator()
        {
            foreach (var pair in children)
            {
                yield return pair.Value;
            }
        }

        public ImmutableDictionary<long, Node> ClearChildren()
        {
            var list = children;
            Interlocked.Exchange(ref children, ImmutableDictionary<long, Node>.Empty);
            return list;
        }

        #region Change Handling
        internal override void AddChild(UpdateTreeCommand command, Node newNode)
        {
            //ImmutableInterlocked.AddOrUpdate(ref children, command.Item.Id, _ => newNode, (k,v) => newNode);
            if (ImmutableInterlocked.TryAdd(ref children, command.Item.Id, newNode))
            {
                newNode.SetParent(this);
                newNode.pipeline?.OnAdded(root, this, newNode, command);
                return;
            }
            //root.log.LogDebug("Tree: AddChild spent {TreeTicks} ticks in tree and {PipelineTicks} in pipeline.", t1, watch.Elapsed.Ticks - t1);

            if (root is SystemTree && command.Action == ETreeAction.Add && command.NodeType == ENodeType.Market)
            {
                root.log.LogDebug("Tree: AddChild failed due to market already in child collection!");
            }
        }

        internal override Node RemoveChild(UpdateTreeCommand command)
        {
            if (!ImmutableInterlocked.TryRemove(ref children, command.Item.Id, out var child))
                return null;
            
            child.pipeline?.OnRemoved(root, this, child, command);
            return child;
            
            //root.log.LogDebug("Tree: RemoveChild spent {TreeTicks} ticks in tree and {PipelineTicks} in pipeline.", t1, watch.Elapsed.Ticks - t1);
        }
        #endregion
    }
}
