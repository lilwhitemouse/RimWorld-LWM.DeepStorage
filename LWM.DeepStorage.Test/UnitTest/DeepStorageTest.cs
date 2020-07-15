using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace LWM.DeepStorage.UnitTest
{
  public abstract class DeepStorageTest
    {
        string FullName;
        public List<DeepStorageTest> Tests = new List<DeepStorageTest>();
        public Dictionary<string, bool> TestResults = new Dictionary<string, bool>();

        public DeepStorageTest()
        {
            FullName = GetType().FullName;
        }

        public virtual void Setup() { }
        public virtual void Cleanup() { }

        public virtual void Run(out bool result)
        {
            if (Enumerable.Any(Tests))
            {
                foreach (DeepStorageTest test in Tests)
                {
                    result = test.Start();
                    TestResults.Add(test.GetType().FullName, result);
                }
            }
            if (TestResults.Any() && TestResults.Values.All(b => b == true))
            {
                result = true;
            }
            else
            {
                result = false;
            }
        }

        public virtual bool Start()
        {
            try
            {
                Setup();
                Run(out bool result);
                Cleanup();

                return result;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
            }
            return false;
        }

        public static int Report(DeepStorageTest tests, ref StringBuilder sb, string indent = "")
        {
            int num = 0;
            if (tests == null || sb == null)
            {
                return 0;
            }

            if (!Enumerable.Any(tests.Tests))
            {
                return 1;
            }

            sb.Append(indent);
            sb.AppendLine($"Number of children tests of {tests.FullName} is {tests.Tests.Count}");
            foreach (DeepStorageTest test in tests.Tests)
            {
                sb.Append(indent);
                sb.Append(' ', 4);
                sb.AppendLine($"{test.FullName}: {tests.TestResults[test.FullName]}");
                num += Report(test, ref sb, string.Concat(indent, "    "));
            }

            sb.AppendLine();
            if (indent.NullOrEmpty())
            {
                sb.AppendLine($"Number of total tests is {num}");
            }

            return num;
        }
    }}
