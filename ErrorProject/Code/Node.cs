using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using RF.Contracts.Models.Keys;
using RF.Domain.Contracts.Models.Keys;
using RF.Domain.MarketCache.Contracts.Enumerations;
using RF.Domain.MarketCache.Contracts.Interfaces.Data;
using RF.Domain.MarketCache.Contracts.Interfaces.Pipelines;
using RF.Domain.MarketCache.Contracts.Models.Data.Base;
using RF.Domain.MarketCache.Contracts.Models.TreeCommands;
using RF.Domain.MarketCache.Trees.Extensions;

namespace RF.Domain.MarketCache.Trees.Nodes.Base
{
    public class Node : INode
    {
        protected internal TreeNode root;
        protected internal Node parentNode;
        protected internal NodeData value;
        protected readonly ENodeType nodeType;
        protected internal long sequenceNumber;
        protected internal readonly INodePipeline pipeline;

        public TreeKey Key { get; }
        public string Topic { get; }

        public Node(ProviderKey provider, TreeNode root, Node parentNode, NodeData value, ENodeType nodeType, INodePipeline pipeline = null)
        {
            Provider = provider;
            this.root = root;
            this.parentNode = parentNode;
            this.value = value;
            this.nodeType = nodeType;
            this.pipeline = pipeline;
            Key = this.GetTreeKey();
            Topic = this.GetTopic(parentNode);
        }

        #region INode
        public long Id
        {
            get => value.Id;
            set => throw new InvalidOperationException();
        }

        public virtual int Count => 0;

        public virtual long SequenceNumber => sequenceNumber;

        public INode Parent => parentNode;

        public ENodeType NodeType => nodeType;

        public NodeData Value => value;

        public NodeData CopyAsSystem(long sequenceNumber, long id, long parentId) => value.CopyAsSystem(sequenceNumber, id, parentId);

        public T GetValue<T>() where T : class
        {
            return value as T;
        }

        public virtual void OnRemoved()
        {
        }
        #endregion

        #region IEnumerable<INode>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public virtual IEnumerator<INode> GetEnumerator()
        {
            return new List<INode>().GetEnumerator();
        }
        #endregion

        #region IMatchable
        public ProviderKey Provider { get; }

        public long SystemEventId { get; private set; }
        public TreeKey SystemKey { get; private set; }
        public bool HomeAwaySwapped { get; private set; }
        public bool Matched => SystemKey != null;

        public void ApplyMatch(long sysEventId, bool homeAwaySwapped = false)
        {
            SystemEventId = sysEventId;
            SystemKey = Key.Copy(sysEventId);
            HomeAwaySwapped = homeAwaySwapped;
        }

        //public TKey GetValueKey<TKey>() where TKey : IEquatable<TKey> => (TKey)SystemKey;

        //public TreeKey GetSystemKey<TKey>(long eventId) where TKey : INodeKey<TKey>
        //{
        //    if (SystemKey != null)
        //        return (TKey)SystemKey;

        //    var systemKey = GetValueKey<TKey>().Adapt(parentId);
        //    //ApplyMatch(eventId, systemKey);
        //    return systemKey;
        //}
        #endregion

        #region IAcceptsVisitor
        public virtual void Accept(IVisitor visitor)
        {
            visitor.Visit(this);
        }
        #endregion

        internal void SetParent(Node node)
        {
            parentNode = node;
        }

        internal Node Process(UpdateTreeCommand command, Node newNode)
        {
            switch (command.Action)
            {
                case ETreeAction.Replace:
                    ReplaceValue(command);
                    Interlocked.Exchange(ref sequenceNumber, command.SequenceNumber);
                    return null;
                case ETreeAction.Add:
                    AddChild(command, newNode);
                    Interlocked.Exchange(ref newNode.sequenceNumber, command.SequenceNumber);
                    return null;
                case ETreeAction.Remove:
                    return RemoveChild(command);
                default:
                    return null;
            }
        }

        internal virtual void ReplaceValue(UpdateTreeCommand command)
        {
            var oldValue = value;
            Interlocked.Exchange(ref value, command.Item);
            pipeline?.OnReplaced(root, this, oldValue, command);
            //root.log.LogDebug("Tree: ReplaceValue spent {TreeTicks} ticks in tree and {PipelineTicks} in pipeline.", t1, watch.Elapsed.Ticks - t1);
        }

        internal virtual void AddChild(UpdateTreeCommand command, Node newNode)
        {
        }

        internal virtual Node RemoveChild(UpdateTreeCommand command)
        {
            return null;
        }
    }
}
