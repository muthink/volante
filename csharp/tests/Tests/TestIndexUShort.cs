namespace Volante
{
    using System;
    using System.Collections;
    using System.Diagnostics;

    public class TestIndexUShort
    {
        public class Record : Persistent
        {
            public long lval;
            public ushort nval; // native value
            public Record(ushort v)
            {
                nval = v;
                lval = (long)v;
            }
            public Record()
            {
            }
        }

        const ushort min = ushort.MinValue;
        const ushort max = ushort.MaxValue;
        const ushort mid = max / 2;

        static byte Clamp(long n)
        {
            long range = max - min;
            long val = (n % range) + (long)min;
            return (byte)val;
        }

        static public TestIndexNumericResult Run(int count, bool altBtree)
        {
            int i;
            Record r = null;
            string dbName = "testnumushort.dbs";
            Tests.SafeDeleteFile(dbName);
            var res = new TestIndexNumericResult()
            {
                Count = count,
                TestName = String.Format("TestIndexUShort, count={0}", count)
            };

            var tStart = DateTime.Now;
            var start = DateTime.Now;

            Storage db = StorageFactory.CreateStorage();
            db.AlternativeBtree = altBtree;
            db.Open(dbName);
            Tests.Assert(null == db.Root);
            var idx = db.CreateIndex<ushort, Record>(false);
            db.Root = idx;
            long val = 1999;
            for (i = 0; i < count; i++)
            {
                val = (3141592621L * val + 2718281829L) % 1000000007L;
                byte idxVal = Clamp(val);
                r = new Record(idxVal);
                idx.Put(idxVal, r);
                if (i % 100 == 0)
                    db.Commit();
            }
            idx.Put(min, new Record(min));
            idx.Put(max, new Record(max));

            Tests.Assert(idx.Count == count + 2);
            db.Commit();
            res.InsertTime = DateTime.Now - start;
            Tests.Assert(idx.Size() == count + 2);

            start = System.DateTime.Now;
            Record[] recs = idx[min, mid];
            foreach (var r2 in recs)
            {
                Tests.Assert(r2.lval >= min && r2.lval <= mid);
            }
            recs = idx[mid, max];
            foreach (var r2 in recs)
            {
                Tests.Assert(r2.lval >= mid && r2.lval <= max);
            }
            ushort prev = min;
            var e1 = idx.GetEnumerator();
            while (e1.MoveNext())
            {
                r = e1.Current;
                Tests.Assert(r.nval >= prev);
                prev = r.nval;
            }

            prev = min;
            foreach (var r2 in idx)
            {
                Tests.Assert(r.nval >= prev);
                prev = r.nval;
            }

            prev = min;
            foreach (var r2 in idx.Range(min, max, IterationOrder.AscentOrder))
            {
                Tests.Assert(r.nval >= prev);
                prev = r.nval;
            }

            prev = max;
            foreach (var r2 in idx.Range(min, max, IterationOrder.DescentOrder))
            {
                Tests.Assert(prev >= r.nval);
                prev = r.nval;
            }

            prev = max;
            foreach (var r2 in idx.Reverse())
            {
                Tests.Assert(prev >= r.nval);
                prev = r.nval;
            }
            long usedBeforeDelete = db.UsedSize;
            recs = idx[min, max];
            foreach (var r2 in recs)
            {
                Tests.Assert(!r2.IsDeleted());
                idx.Remove(r2.nval, r2);
                r2.Deallocate();
            }
            Tests.Assert(idx.Count == 0);
            db.Commit();
            long usedAfterDelete = db.UsedSize;
            db.Gc();
            db.Commit();
            long usedAfterGc = db.UsedSize;
            db.Close();
            res.ExecutionTime = DateTime.Now - tStart;
            res.Ok = Tests.FinalizeTest();
            return res;
        }
    }
}