Язык фильтрации позволяет включать задачи только для различных тегов

(строки языка приводятся в кавычках, как они должны вставляться в параметры командной строки)
'1'

Включаются только задачи, имеющие тег 1

''
Все задачи

'-1'
Все задачи, кроме задач с тегом -1

'<0 1'
Все задачи с тегом 1 и параметром длительности duration <= 0
'-1 <0 1'
То же. Исключаются задачи с тегом 1, а затем добавляются задачи с длительностью не более 0 и тегом 1
'<0 -1'



'1 <0 -1'