using System;
using System.IO;
using System.Text;
using CSVUtils;

namespace CSVLang
{
    internal enum ExpressionType
    {
        And,
        Or,
        Not,
        Atom,
        Print,
        Set,
        DoNothing
    };

    public class Expression
    {
        private ExpressionType type;
        private Expression[] subExprs;

        //fields for type Atom
        private char aRelation;
        private string fieldName;
        private string fieldValue;
        private char castAs;

        public Expression(string toParse)
        {
            if (string.IsNullOrWhiteSpace(toParse))
                throw new Exception("Cannot form expression from empty input");
            int stack;
            switch (toParse[0])
            {
                default:
                    StringBuilder sb = new StringBuilder();
                    type = ExpressionType.Atom;
                    castAs = 's';
                    AtomReader.State s = AtomReader.State.Start;
                    foreach (char c in toParse)
                    {
                        s = AtomReader.NextState(s, c);
                        switch (s)
                        {
                            case AtomReader.State.ReadingFieldName:
                            case AtomReader.State.ReadingFieldValue:
                                sb.Append(c);
                                break;
                            case AtomReader.State.ReadRelation:
                                aRelation = c;
                                fieldName = sb.ToString();
                                sb = new StringBuilder();
                                break;
                            case AtomReader.State.ReadFieldValue:
                                fieldValue = sb.ToString();
                                break;
                            case AtomReader.State.ReadCastType:
                                castAs = c;
                                break;
                            case AtomReader.State.InvalidSyntax:
                                throw new Exception(string.Format("Invalid syntax: {0}", toParse));

                        }
                    }
            //        Console.WriteLine("field name: {0}\nfield value: {1}\nrelation: {2}\ncast as: {3}", fieldName, fieldValue, aRelation, castAs);
            //Console.WriteLine("=================================");
                    break;
                case '(':
                    BinaryExprReader.State r = BinaryExprReader.State.Start;
                    stack = 0;
                    StringBuilder rb = new StringBuilder();
                    string expr1 ="", expr2="";
                    char op='x';
                    subExprs = new Expression[2];
                    foreach (char c in toParse)
                    {
                        r = BinaryExprReader.NextState(r, c, ref stack);
                        switch (r)
                        {
                            case BinaryExprReader.State.ReadingExpr1:
                            case BinaryExprReader.State.ReadingExpr2:
                                rb.Append(c);
                                break;
                            case BinaryExprReader.State.ReadCloseBracket1:
                                expr1 = rb.ToString();
                                rb = new StringBuilder();
                                break;
                            case BinaryExprReader.State.ReadOperator:
                                op = c;
                                switch(c)
                                {
                                    case '&':
                                        type = ExpressionType.And;
                                        break;
                                    case '|':
                                        type = ExpressionType.Or;
                                        break;
                                }
                                break;
                            case BinaryExprReader.State.ReadCloseBracket2:
                                expr2 = rb.ToString();
                                break;
                            case BinaryExprReader.State.InvalidSyntax:
                                throw new Exception(string.Format("Invalid syntax: {0}", toParse));
                        }
                    }
                    //Console.WriteLine("expression1: {0}\noperator: {1}\nexpression2: {2}", expr1, op, expr2);
            //Console.WriteLine("=================================");
                    subExprs[0] = new Expression(expr1);
                    subExprs[1] = new Expression(expr2);
                    break;
                case '!':
                    stack = 0;
                    string expr = "";
                    NotReader.State nrs = NotReader.State.Start;
                    type = ExpressionType.Not;
                    sb = new StringBuilder();
                    subExprs = new Expression[1];
                    foreach (char c in toParse)
                    {
                        nrs = NotReader.NextState(nrs, c, ref stack);
                        switch (nrs)
                        {
                            case NotReader.State.ReadingExpr:
                                sb.Append(c);
                                break;
                            case NotReader.State.ReadExpr:
                                expr = sb.ToString();
                                break;
                            case NotReader.State.InvalidSyntax:
                                throw new Exception(string.Format("Invalid syntax: {0}", toParse));
                        }
                    }
                    //Console.WriteLine("expression: {0}",expr);
            //Console.WriteLine("=================================");
            subExprs[0] = new Expression(expr);
                    break;
            }
        }

        public bool Eval(CSVRow row)
        {
            switch (type)
            {
                case ExpressionType.Atom:
                    //return row.GetEntry(fieldName).Equals(fieldValue);
                    return EvalAsAtom(row);
                case ExpressionType.And:
                    return subExprs[0].Eval(row) && subExprs[1].Eval(row);
                case ExpressionType.Or:
                    return subExprs[0].Eval(row) || subExprs[1].Eval(row);
                case ExpressionType.Not:
                    return !subExprs[0].Eval(row);
                default:
                    return true;
            }
        }

        private bool EvalAsAtom(CSVRow row)
        {
            switch (castAs)
            {
                default:
                    switch (aRelation)
                    {
                        case '=':
                            return row.GetEntry(fieldName).Equals(fieldValue);
                        case '<':
                            return row.GetEntry(fieldName).CompareTo(fieldValue) < 0;
                        case '>':
                            return row.GetEntry(fieldName).CompareTo(fieldValue) > 0;
                        default:
                            return false;
                    }
                case 'd':
                    DateTime d1, d2;
                    if (!DateTime.TryParse(GetFinalDateIfEmpty(row.GetEntry(fieldName)), out d1))
                        throw new Exception(string.Format("Unable to parse {0} as date!", row.GetEntry(fieldName)));
                    if (!DateTime.TryParse(GetFinalDateIfEmpty(fieldValue), out d2))
                        throw new Exception(string.Format("Unable to parse {0} as date!", fieldValue));
                    switch (aRelation)
                    {
                        case '=':
                            return d1 == d2;
                        case '<':
                            return d1 < d2;
                        case '>':
                            return d1 > d2;
                        default:
                            return false;
                    }
            }

        }
        private string GetFinalDateIfEmpty(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "31/12/2999" : s;
        }
    }

    internal class AtomReader
    {
        public enum State
        {
            Start,
            ReadingFieldName,
            ReadRelation,
            ReadingFieldValue,
            ReadFieldValue,
            ReadCastType,
            InvalidSyntax
        };

        public static State[] AcceptStates =
        {
            State.ReadFieldValue,
            State.ReadCastType
        };

        public static State NextState(State s, char c)
        {
            State next = State.InvalidSyntax;
            switch (s)
            {
                case State.Start:
                case State.ReadingFieldName:
                    switch (c)
                    {
                        case '=':
                        case '<':
                        case '>':
                            next = State.ReadRelation;
                            break;
                        default:
                            next = State.ReadingFieldName;
                            break;
                    }
                    break;
                case State.ReadRelation:
                case State.ReadingFieldValue:
                    switch (c)
                    {
                        case '.':
                            next = State.ReadFieldValue;
                            break;
                        default:
                            next = State.ReadingFieldValue;
                            break;
                    }
                    break;
                case State.ReadFieldValue:
                    switch (c)
                    {
                        case 's':
                        case 'i':
                        case 'd':
                            next = State.ReadCastType;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadCastType:
                    switch (c)
                    {
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.InvalidSyntax:
                    break;
            }
            return next;
        }
    }

    internal class BinaryExprReader
    {
        public enum State
        {
            Start,
            ReadOpenBracket1,
            ReadingExpr1,
            ReadCloseBracket1,
            ReadOperator,
            ReadOpenBracket2,
            ReadingExpr2,
            ReadCloseBracket2,
            InvalidSyntax

        };

        public static State NextState(State s, char c, ref int stack)
        {
            State next = State.InvalidSyntax;
            switch (s)
            {
                case State.Start:
                    if (stack != 0)
                        next = State.InvalidSyntax;
                    else
                        switch(c)
                        {
                            case '(':
                                stack += 1;
                                next = State.ReadOpenBracket1;
                                break;
                            default:
                                next = State.InvalidSyntax;
                                break;
                        }
                    break;
                case State.ReadOpenBracket1:
                case State.ReadingExpr1:
                    switch(c)
                    {
                        case '(':
                            stack += 1;
                            next = State.ReadingExpr1;
                            break;
                        case ')':
                            stack -= 1;
                            if (stack < 1)
                                next = State.ReadCloseBracket1;
                            else
                                next = State.ReadingExpr1;
                            break;
                        default:
                            next = State.ReadingExpr1;
                            break;

                    }
                    break;
                case State.ReadCloseBracket1:
                    switch(c)
                    {
                        case '&':
                        case '|':
                            next = State.ReadOperator;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadOperator:
                    if (stack != 0)
                        next = State.InvalidSyntax;
                    else
                        switch(c)
                        {
                            case '(':
                                stack += 1;
                                next = State.ReadOpenBracket2;
                                break;
                            default:
                                next = State.InvalidSyntax;
                                break;
                        }
                    break;
                case State.ReadOpenBracket2:
                case State.ReadingExpr2:
                    switch(c)
                    {
                        case '(':
                            stack += 1;
                            next = State.ReadingExpr2;
                            break;
                        case ')':
                            stack -= 1;
                            if (stack < 1)
                                next = State.ReadCloseBracket2;
                            else
                                next = State.ReadingExpr2;
                            break;
                        default:
                            next = State.ReadingExpr2;
                            break;

                    }
                    break;
                case State.ReadCloseBracket2:
                    switch(c)
                    {
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                default:
                    next = State.InvalidSyntax;
                    break;
            }
            return next;
        }
    }

    internal class NotReader
    {
        public enum State
        {
            Start,
            ReadNot,
            ReadOpenBracket,
            ReadingExpr,
            ReadExpr,
            InvalidSyntax
        };

        public static State NextState(State s, char c, ref int stack)
        {
            State next =  State.InvalidSyntax;
            switch(s)
            {
                case State.Start:
                    switch(c)
                    {
                        case '!':
                            next = State.ReadNot;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadNot:
                    switch(c)
                    {
                        case '(':
                            next = State.ReadOpenBracket;
                            stack += 1;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadOpenBracket:
                case State.ReadingExpr:
                    switch(c)
                    {
                        case '(':
                            stack += 1;
                            next = State.ReadingExpr;
                            break;
                        case ')':
                            stack -= 1;
                            if (stack < 1)
                                next = State.ReadExpr;
                            else
                                next = State.ReadingExpr;
                            break;
                        default:
                            next = State.ReadingExpr;
                            break;
                    }
                    break;
                case State.ReadExpr:
                case State.InvalidSyntax:
                    next = State.InvalidSyntax;
                    break;
            }
            return next;
        }
    }

    public class ActionExpression
    {
        private ExpressionType type;
        private ActionExpression next;

        private string fieldName, fieldValue;

        public ActionExpression(string toParse)
        {
            //ActionExpression(toParse, 0);
        }

        public ActionExpression(string toParse, int index)
        {
            type = ExpressionType.DoNothing;
            next = null;
            ActionsReader.State s = ActionsReader.State.Start;
            StringBuilder sb = new StringBuilder();
            for (int i=index;i<toParse.Length;i++)
            {
                s = ActionsReader.NextState(s, toParse[i]);
                switch(s)
                {
                    case ActionsReader.State.ReadPrint:
                        type = ExpressionType.Print;
                        next = new ActionExpression(toParse, i + 1);
                        goto loopEnd;
                    case ActionsReader.State.ReadSet:
                        type = ExpressionType.Set;
                        break;
                    case ActionsReader.State.ReadingFieldName:
                    case ActionsReader.State.ReadingFieldValue:
                        sb.Append(toParse[i]);
                        break;
                    case ActionsReader.State.ReadFieldName:
                        fieldName = sb.ToString();
                        sb = new StringBuilder();
                        break;
                    case ActionsReader.State.ReadFieldValue:
                        fieldValue = sb.ToString();
                        next = new ActionExpression(toParse, i + 1);
                        goto loopEnd;
                }
            }
loopEnd:;
            //Console.WriteLine(s);
        }

        public void Act(CSVRow row)
        {
            switch(type)
            {
                case ExpressionType.Print:
                    Console.WriteLine(row);
                    break;
                case ExpressionType.Set:
                    row.SetEntry(fieldName, fieldValue);
                    break;
            }
            if (next != null)
                next.Act(row);
        }
    }

    internal class ActionsReader
    {
        public enum State
        {
            Start,
            ReadingPrint,
            ReadPrint,
            ReadSet,
            ReadingFieldName,
            ReadFieldName,
            ReadingFieldValue,
            ReadFieldValue,
            InvalidSyntax
        };

        public static State NextState(State s, char c)
        {
            State next = State.InvalidSyntax;
            switch(s)
            {
                case State.Start:
                case State.ReadPrint:
                case State.ReadFieldValue:
                    switch (c)
                    {
                        case 'p':
                            next = State.ReadingPrint;
                            break;
                        case 's':
                            next = State.ReadSet;
                            break;
                        default:
                            next = State.Start;
                            break;
                    }
                    break;
                case State.ReadingPrint:
                    switch (c)
                    {
                        case ';':
                            next = State.ReadPrint;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadSet:
                    switch (c)
                    {
                        case ' ':
                            next = State.ReadSet;
                            break;
                        case '=':
                            next = State.ReadFieldName;
                            break;
                        default:
                            next = State.ReadingFieldName;
                            break;
                    }
                    break;
                case State.ReadingFieldName:
                    switch (c)
                    {
                        case '=':
                            next = State.ReadFieldName;
                            break;
                        default:
                            next = State.ReadingFieldName;
                            break;
                    }
                    break;
                case State.ReadFieldName:
                case State.ReadingFieldValue:
                    switch (c)
                    {
                        case ';':
                            next = State.ReadFieldValue;
                            break;
                        default:
                            next = State.ReadingFieldValue;
                            break;
                    }
                    break;
                default:
                    next = State.InvalidSyntax;
                    break;
            }
            return next;
        }
    }

    public class Program
    {
        private Expression query;
        private ActionExpression trueAction;
        private ActionExpression falseAction;

        public Program(string programSource)
        {
            string queryString="",tAction="",fAction="";
            ProgramReader.State s = ProgramReader.State.Start;
            StringBuilder sb = null;
            foreach (char c in programSource)
            {
                s = ProgramReader.NextState(s, c);
                switch (s)
                {
                    case ProgramReader.State.StartQueryRead:
                    case ProgramReader.State.StartTActionRead:
                    case ProgramReader.State.StartFActionRead:
                        sb = new StringBuilder();
                        break;
                    case ProgramReader.State.ReadingQuery:
                    case ProgramReader.State.ReadingTAction:
                    case ProgramReader.State.ReadingFAction:
                        sb.Append(c);
                        break;
                    case ProgramReader.State.ReadQuery:
                        queryString = sb.ToString();
                        break;
                    case ProgramReader.State.ReadTAction:
                        tAction = sb.ToString();
                        break;
                    case ProgramReader.State.ReadFAction:
                        fAction = sb.ToString();
                        break;
                    case ProgramReader.State.InvalidSyntax:
                        throw new Exception("Invalid Syntax!");
                }
            }
            query = new Expression(queryString.Trim());
            trueAction = new ActionExpression(tAction.Trim(), 0);
            falseAction = new ActionExpression(fAction.Trim(), 0);
        }

        public void Execute(CSVRow row)
        {
            if (query.Eval(row))
                trueAction.Act(row);
            else
                falseAction.Act(row);
        }
    }

    internal class ProgramReader
    {
        public enum State
        {
            Start,
            ReadQ,
            StartQueryRead,
            ReadingQuery,
            ReadQuery,
            ReadyForT,
            ReadT,
            StartTActionRead,
            ReadingTAction,
            ReadTAction,
            ReadyForF,
            ReadF,
            StartFActionRead,
            ReadingFAction,
            ReadFAction,
            InvalidSyntax
        };

        public static State NextState(State s, char c)
        {
            State next = State.InvalidSyntax;

            switch(s)
            {
                case State.Start:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.Start;
                            break;
                        case 'q':
                            next = State.ReadQ;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadQ:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadQ;
                            break;
                        case '{':
                            next = State.StartQueryRead;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.StartQueryRead:
                case State.ReadingQuery:
                    switch(c)
                    {
                        case '}':
                            next = State.ReadQuery;
                            break;
                        default:
                            next = State.ReadingQuery;
                            break;
                    }
                    break;
                case State.ReadQuery:
                case State.ReadyForT:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadyForT;
                            break;
                        case 't':
                            next = State.ReadT;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadT:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadT;
                            break;
                        case '{':
                            next = State.StartTActionRead;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.StartTActionRead:
                case State.ReadingTAction:
                    switch(c)
                    {
                        case '}':
                            next = State.ReadTAction;
                            break;
                        default:
                            next = State.ReadingTAction;
                            break;
                    }
                    break;
                case State.ReadTAction:
                case State.ReadyForF:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadyForF;
                            break;
                        case 'f':
                            next = State.ReadF;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.ReadF:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadF;
                            break;
                        case '{':
                            next = State.StartFActionRead;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
                case State.StartFActionRead:
                case State.ReadingFAction:
                    switch(c)
                    {
                        case '}':
                            next = State.ReadFAction;
                            break;
                        default:
                            next = State.ReadingFAction;
                            break;
                    }
                    break;
                case State.ReadFAction:
                    switch(c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            next = State.ReadFAction;
                            break;
                        default:
                            next = State.InvalidSyntax;
                            break;
                    }
                    break;
            }
            return next;
        }
    }

    public class Test
    {
        public static void Main(string[] args)
        {    
            /*Expression e;
            CSVData csv;
            ActionExpression trueAction;
            ActionExpression falseAction;
            try
            {
                using (StreamReader sr = new StreamReader(args[0]))
                    e = new Expression(sr.ReadToEnd().Trim());
                using (StreamReader sr = new StreamReader(args[1]))
                    trueAction = new ActionExpression(sr.ReadToEnd().Trim(), 0);
                using (StreamReader sr = new StreamReader(args[2]))
                    falseAction = new ActionExpression(sr.ReadToEnd().Trim(), 0);
                csv = (new CSVReader()).ReadAll(Console.In);
                Console.WriteLine(csv.Header);
                foreach (CSVRow row in csv)
                {
                    if (e.Eval(row))
                        trueAction.Act(row);
                    else
                        falseAction.Act(row);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }*/
            Program p;
            CSVData csv;
            try
            {
                using (StreamReader sr = new StreamReader(args[0]))
                    p = new Program(sr.ReadToEnd());
                csv = (new CSVReader()).ReadAll(Console.In);
                Console.WriteLine(csv.Header);
                foreach (CSVRow row in csv)
                    p.Execute(row);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
    }
}
