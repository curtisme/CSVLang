# CSVLang
Interpreter for a simple specialised language for doing specific kinds of CSV manipulations. 

Requires my CSVUtils library which I currently have in my DotNetUtilities repo.

### The language:

Suppose we have a CSV file, test.csv

```
col1,col2,col with spaces
abc,def,12/09/1889
test1,test2,01/01/2000
```
An example 'program' might look something like this:

```
q {
    col with spaces<31/12/1999.d
}

t {
    p;
    s col2=foo;
    p;
}

f {
    p;
}
```
we would save this to a file 'source' and run

```
CSVLang.exe source < test.csv
```

whic outputs
```
col1,col2,col with spaces
abc,def,12/09/1889
abc,foo,12/09/1889
test1,test2,01/01/2000
```

