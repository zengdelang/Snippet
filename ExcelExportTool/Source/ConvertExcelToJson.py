import xlrd
import json
import sys
import os
import platform

from imp import find_module
from imp import load_module
from colorful_cmd import ColorfulCMD
from plugin_manager import PluginManager
from json_utility import loadJson

colorfulCMD = ColorfulCMD()
# 打印错误信息时程序立即结束
def printErrorMsg(msg):
    print(colorfulCMD.red(msg))
    quit()

def printWarningMsg(msg):
    print(colorfulCMD.yellow(msg))

def getFilePath(path):
    if(platform.system() =="Windows"):
        return path.replace("/","\\")
    else:
         return path.replace("\\","/")

def isValidExcelFile(path):
    try:
        xlrd.open_workbook(path)
        return True
    except:
        return False

def getExcelFilesFromDir(path, fileList):
    for root, dirs, files in os.walk(path):
        for f in files:
            fullPath = getFilePath(os.path.join(root, f))
            if isValidExcelFile(fullPath):
                fileList.append(fullPath)      

        for dir in dirs:
            getExcelFilesFromDir(dir, fileList)         

if __name__ == '__main__':
    try:
        #当前exe文件所在的目录的绝对路径   
        curExeParentPath = os.path.abspath(sys.argv[0]+os.path.sep+"..")
        configJsonPath = os.path.join(curExeParentPath, "Config/Config.json")
        configJsonPath = getFilePath(configJsonPath)

        if not os.path.exists(configJsonPath):
            printErrorMsg("找不到配置文件，路径: "+configJsonPath)

        jsonConfig, result = loadJson(configJsonPath)
        if jsonConfig == None:
            printErrorMsg(result)

        exportRootPath = None
        if "relativeExportPath" in jsonConfig and jsonConfig["relativeExportPath"] != '':
            exportRootPath = os.path.join(curExeParentPath, jsonConfig["relativeExportPath"])
            exportRootPath = getFilePath(exportRootPath)
        elif "absoluteExprotPath" in jsonConfig and jsonConfig["absoluteExprotPath"] != '':
            exportRootPath = jsonConfig["absoluteExprotPath"] 
            exportRootPath = getFilePath(exportRootPath)
        else:
            printErrorMsg("找不到导出配置文件保存路径，请先配置Config.json的relativeExportPath或absoluteExprotPath属性")

        if len(sys.argv) <= 1:
            printErrorMsg("没有传入待处理的Excel文件路径或包含Excel文件的文件夹路径，程序无法处理")

        path = sys.argv[1]
        if os.path.isdir(path):
            excelFiles = []
            getExcelFilesFromDir(path, excelFiles)
            if len(excelFiles) == 0:
                printWarningMsg("文件夹下不存在有效的Excel文件，无需导出, Path: " + path)
                quit()
            else:
                print("==========")

        elif os.path.isfile(path):
            if not isValidExcelFile(path):
                printErrorMsg("不是有效的Excel文件，程序无法处理")
                
            #有效的excel文件
            print("===========")
        else:
            printErrorMsg("无效的输入路径，程序无法处理")


            # pluginManager = PluginManager()
            # pluginManager.LoadAllPlugin()
            # pluginManager.ExecAllChecker()
    finally:
        os.system('pause')
    
    