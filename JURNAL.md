# Laboratorul Î4 · Oprisor Paul · nr. matricol 121200067 · varianta A2 · B2

| Parametru               | Valoarea mea                    |
| ----------------------- | ------------------------------- |
| A (ultima cifră: 7)     | A2                              |
| B (penultima cifră: 6)  | B2                              |
| V1 · cărți pe pagină    | 7                               |
| V2 · sortarea implicită | număr de comenzi, descrescător  |
| V3 · filtrul din S3     | an: de la – până la             |
| V4 · indicatorul din S2 | anul primei și al ultimei cărți |

## S1 · Comenzile

- Overposting: am folosit Bind, cu CustomerID,BookID,OrderDate la Create.Order are puține câmpuri și Bind e cel mai simplu, nu mai trebuie o clasă în plus ca la ViewModel.

La Create nu se salva nimic, fără nicio eroare. Customer și Book sunt non-nullable și MVC le cerea ca obligatorii. Am rezolvat cu ModelState.Remove(nameof(Order.Customer)) și la fel pentru Book.

## S2 · Pagina autorului

- V4 (anul primei și al ultimei cărți), în SQL:

```sql
SELECT MIN("b"."Year"), MAX("b"."Year")
FROM "Book" AS "b"
WHERE "b"."AuthorID" = @id
GROUP BY "b"."AuthorID"
```

- De ce OrderBy(b => b.Author.FullName) dă excepție: FullName e calculat în C#, nu există coloană în baza de date, deci nu îl poate traduce în SQL.
- Rezolvare: sortez după Author.LastName, apoi Author.FirstName.

## S3 · Filtrul pe ani

- yearFrom și yearTo, aplicate în SQL împreună cu căutarea, sortarea și paginarea:

```sql
SELECT ... FROM "Book" AS "b"
WHERE instr("b"."Title", @searchString) > 0
  AND "b"."Year" >= @yearFrom AND "b"."Year" <= @yearTo
ORDER BY "b"."Price" DESC, "b"."ID"
LIMIT @p4 OFFSET @p
```

am căutat „a”, cu ani de la 1900 până la 2020, sortat după preț descrescător

## S4 · Folosirea AI

- La ce pași:m-a ajutat la configurarea repo-ului si la scriptul pentru scaffolding, neavand IDE
- Exemplu de cerere: „ruleaza scriptul pentru scaffolding”
- Unde a greșit: mi-a zis să instalez .NET 8 din cauza lui global.json, dar proiectul era net10.0 și SDK-ul 10 era deja instalat. Mi-am dat seama din eroarea de la dotnet run
