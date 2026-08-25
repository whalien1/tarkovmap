# TarkovMap MapData Builder v1.0.0

这是 TarkovMap 的地图数据维护工具，不是地图客户端，也不是地图编辑器。

## 启动

1. 保持 ZIP 内所有文件在同一个目录，不要只复制 EXE。
2. 电脑需安装 .NET 10 Desktop Runtime x64。
3. 双击 `TarkovMap.MapDataBuilder.exe`。
4. 检查“正式 Data”是否指向 TarkovMap 客户端的 `Data` 文件夹；如果未自动识别，点击“选择”。

## 推荐操作顺序

1. 获取数据。
2. 构建 MapData。
3. 查看变化并重新校验。
4. 在独立客户端实测海关、中心区、街区和实验室，以及所有超过 30% 的数量变化。
5. 只有实际验收通过后，项目所有者才能点击“确认验收”。
6. 导出 ZIP。
7. 关闭 TarkovMap 客户端，再应用到正式程序。
8. 启动 TarkovMap 检查；如果异常，关闭客户端后使用“恢复上一版本”。

## 安全规则

- 构建只写入“文档/TarkovMap MapData Builds”，不会直接覆盖正式 Data。
- 已存在的测试目录不会被自动清空或覆盖。
- 普通 Validation Error 不能通过人工验收绕过。
- 正式应用只保留最近一个已验证备份，路径为客户端旁的 `Data.backup`。
- 恢复完成后备份槽会清空。
- 不要手工删除或移动 `Data`、`Data.backup`，也不要使用来源不明的 ZIP。

详细技术说明和数据许可见同目录 `NOTICE.md` 及项目仓库文档。
