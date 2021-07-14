Comic
\n0		    (Item seperator)
\n1			(Property seperator)
1			Id
2			CollectionId
3			Title
4			Title2
5			(Tags)
	\n2			(Tag Seperator)
	\n3			(Property Seperator)
	1			TagName
	2			Tag 1/Tag 2/...
6			Directory

ComicRecord
1			Id
\n			(Property seperator)
2			Rating
3			Progress
\n\n		(Item seperator)
...

FavoritesData
\n0			(Start of a new item)
\n1[f/i]	Type(Folder/Item)
\n2			Content
\n3			Name
\n8			Move down
\n9			Move up

AppSettingsData
Boolean		Save history
Int16		Items to keep
Int32		Comics folder string length
String		Comics folder