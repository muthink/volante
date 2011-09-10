namespace Volante
{
    using System;

    public class TestMultiFieldResult : TestResult
    {
        public TimeSpan InsertTime;
        public TimeSpan IndexSearchTime;
        public TimeSpan IterationTime;
        public TimeSpan RemoveTime;
    }

    public class TestMultiFieldIndex : ITest
    {
        class Record : Persistent
        {
            internal String strKey;
            internal int intKey;
        }

        public void Run(TestConfig config)
        {
            int count = config.Count;
            var res = new TestMultiFieldResult();
            config.Result = res;

            DateTime start = DateTime.Now;

            IDatabase db = config.GetDatabase();
            IMultiFieldIndex<Record> root = (IMultiFieldIndex<Record>)db.Root;
            Tests.Assert(root == null);
            root = db.CreateFieldIndex<Record>(new string[] { "intKey", "strKey" }, IndexType.Unique);
            db.Root = root;

            foreach (var key in Tests.KeySeq(count))
            {
                Record rec = new Record();
                rec.intKey = (int)((ulong)key >> 32);
                rec.strKey = Convert.ToString((int)key);
                root.Put(rec);
            }
            Tests.Assert(root.Count == count);
            db.Commit();
            Tests.Assert(root.Count == count);
            res.InsertTime = DateTime.Now - start;

            start = DateTime.Now;
            int minKey = Int32.MaxValue;
            int maxKey = Int32.MinValue;
            foreach (var key in Tests.KeySeq(count))
            {
                int intKey = (int)((ulong)key >> 32);
                String strKey = Convert.ToString((int)key);
                Record rec = root.Get(new Key(new Object[] { intKey, strKey }));
                Tests.Assert(rec != null && rec.intKey == intKey && rec.strKey.Equals(strKey));
                if (intKey < minKey)
                    minKey = intKey;
                if (intKey > maxKey)
                    maxKey = intKey;
            }
            res.IndexSearchTime = DateTime.Now - start;

            start = DateTime.Now;
            int n = 0;
            string prevStr = "";
            int prevInt = minKey;
            foreach (Record rec in root.Range(new Key(minKey, ""),
                                              new Key(maxKey + 1, "???"),
                                              IterationOrder.AscentOrder))
            {
                Tests.Assert(rec.intKey > prevInt || rec.intKey == prevInt && rec.strKey.CompareTo(prevStr) > 0);
                prevStr = rec.strKey;
                prevInt = rec.intKey;
                n += 1;
            }
            Tests.Assert(n == count);

            n = 0;
            prevInt = maxKey + 1;
            foreach (Record rec in root.Range(new Key(minKey, "", false),
                                              new Key(maxKey + 1, "???", false),
                                              IterationOrder.DescentOrder))
            {
                Tests.Assert(rec.intKey < prevInt || rec.intKey == prevInt && rec.strKey.CompareTo(prevStr) < 0);
                prevStr = rec.strKey;
                prevInt = rec.intKey;
                n += 1;
            }
            Tests.Assert(n == count);
            res.IterationTime = DateTime.Now - start;
            start = DateTime.Now;
            foreach (var key in Tests.KeySeq(count))
            {
                int intKey = (int)((ulong)key >> 32);
                String strKey = Convert.ToString((int)key);
                Record rec = root.Get(new Key(new Object[] { intKey, strKey }));
                Tests.Assert(rec != null && rec.intKey == intKey && rec.strKey.Equals(strKey));
                Tests.Assert(root.Contains(rec));
                root.Remove(rec);
                rec.Deallocate();
            }
            Tests.Assert(!root.GetEnumerator().MoveNext());
            Tests.Assert(!root.Reverse().GetEnumerator().MoveNext());
            res.RemoveTime = DateTime.Now - start;
            db.Close();
        }
    }
}
