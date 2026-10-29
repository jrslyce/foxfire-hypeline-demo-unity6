using System;
using System.IO;
using Foxfire;
class JournalTests
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"foxfire-journal-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,"receipts.tsv");
            var j=new ReceiptJournal(3,path);
            for(int i=0;i<50;i++)
            {
                j.Remember(new Receipt("id-"+i,"executed"));
                var reloaded=new ReceiptJournal(3,path);
                Check(reloaded.Count==Math.Min(3,i+1),"bounded replay count");
                Check(reloaded.Find("id-"+i).status=="executed","latest receipt persisted");
                if(i>=3) Check(reloaded.Find("id-"+(i-3))==null,"old receipt evicted");
                Check(File.ReadAllLines(path).Length<=6,"bounded disk history through rollover");
            }
            j.Remember(new Receipt("id-49","rejected","updated"));
            Check(new ReceiptJournal(3,path).Find("id-49").reason=="updated","updated receipt replay");
            File.AppendAllText(path,"interrupted\texecuted\tpartial");
            j=new ReceiptJournal(3,path);
            Check(j.Find("interrupted")==null,"ignore unterminated crash record");
            j.Remember(new Receipt("after-crash","executed"));
            Check(new ReceiptJournal(3,path).Find("after-crash")!=null,"repair tail before appending");
            File.WriteAllText(path,"old\texecuted\t\nnew\trejected\told_format\n");
            j=new ReceiptJournal(3,path);
            Check(j.Count==2 && j.Find("new").reason=="old_format","legacy snapshot compatibility");
            path=Path.Combine(root,"one.tsv"); j=new ReceiptJournal(1,path);
            for(int i=0;i<5;i++) j.Remember(new Receipt("one-"+i,"executed"));
            Check(new ReceiptJournal(1,path).Find("one-4")!=null,"capacity one compaction");
            Console.WriteLine("PASS journal rollover, replay, update, truncation, legacy and capacity-one tests");
        }
        finally { Directory.Delete(root,true); }
    }
}
