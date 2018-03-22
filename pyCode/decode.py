#-*- encoding: utf-8 -*-  

import base64
import sys
import os

print('输入文件名'.decode('utf-8').encode('gbk'))

filePath = sys.stdin.readline()
filePath = filePath.replace('\\', '/').strip('\n')
file = open(filePath, 'r')
str = file.read().replace('\n', '').replace('\r', '').strip('\n').strip('\r')

target = open(os.path.dirname(filePath) + "/testcode.7z",'wb') 
result = base64.b64decode(str)

target.write(result)
file.close()
target.close()