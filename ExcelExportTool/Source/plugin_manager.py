
# 插件不需要报错，是可选的
class PluginManager:
    #静态变量配置插件路径
    __CheckPluginPath = '\Plugins\\Checker'

    def __init__(self):
        self.__CheckerPlugins = []       #数据检查器插件集合
        self.__ExporterPlugins = []      #数据导出器插件集合
        self.__PostprocessorPlugins = [] #全部数据导出完成的后处理插件集合

    #递归检测插件路径下的所有插件，并将它们存到内存中
    def loadAllPlugin(self):
        self.LoadCheckerPlugins(self.__CheckPluginPath)

    def loadCheckerPlugins(self,pluginPath):
        path = os.path.abspath(sys.argv[0]+os.path.sep+"..")+pluginPath
        if not os.path.isdir(path):
            raise EnvironmentError('%s is not a directory' % path)

        items = os.listdir(path)
        for item in items:
            if os.path.isdir(os.path.join(path,item)):
                self.LoadCheckerPlugins(os.path.join(pluginPath,item))
            else:
                if item.endswith('.py') and item != '__init__.py':
                    moduleName = item[:-3]
                    if moduleName not in sys.modules:
                        fileHandle, filePath,dect = find_module(moduleName,[path])
                    else:
                        print("fuck 存在同名的Checker插件")
                        return
                    try:
                        moduleObj = load_module(moduleName,fileHandle,filePath,dect)
                        #如果没有函数，警告
                        self.__CheckerPlugins.append(moduleObj)
                    finally:
                        if fileHandle : fileHandle.close()

    def execAllChecker(self):
        #异常处理
        for checker in self.__CheckerPlugins:
            checker.CheckExcelData()