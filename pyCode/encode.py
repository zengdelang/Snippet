#-*- encoding: utf-8 -*-  


import base64
import sys
import os

print('输入文件名'.decode('utf-8').encode('gbk'))

filePath = sys.stdin.readline()
filePath = filePath.replace('\\', '/').strip('\n')
file = open(filePath, 'rb')

target = open(os.path.dirname(filePath) + "/testcode.txt",'wb') 
result =base64.b64encode(file.read())

list = []
for i in xrange(0, len(result), 400):
    list.append(result[i:i+400])

for i in list:
    target.write(i+"\n")

file.close()
target.close()