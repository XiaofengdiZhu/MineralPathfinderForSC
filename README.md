# Mineral Pathfinder For Survivalcraft 生存战争寻矿器

## Download 下载

[Click To Download 点此下载](https://github.com/XiaofengdiZhu/MineralPathfinderForSC/releases)

## Introduction 介绍

Video: [Youtube](https://youtu.be/5064abPKq3w)

This mod adds a new block: the Mineral Pathfinder.  
You can make a Mineral Pathfinder from 9 kinds of mineral chunks on a crafting table. Place it on the ground and connect it to a button or other power source; each time it receives a high signal, it performs a scan.  
It will only scan for exposed targets on the ground or walls, and cannot find targets hidden in rocks or unreachable targets.  
When the targets are successfully found, it draws a thin and bright yellow line on the ground, leading to the destination (the line will be cleared when you leave the world).  
You can adjust its settings via the edit dialog.

On the left side of the edit dialog, you can choose which ore blocks you want to scan for, or any other target blocks, or the last sleep location and the last death location.  
The two buttons at the top allow you to adjust the sorting of the block selector, and filter blocks by name or category.  
The five buttons at the bottom are:

1. Select / Deselect (or simply double-click a block)
2. Favorite a block
3. View detailed information about a block
4. Manually input a block value
5. View all selected blocks and their values in a list

On the right side of the dialog, there are three settings:

1. Result Group Count: the count of target block groups to scan for. The path line will connect all results together. Default is 1, maximum is 64.
2. Scan Range: measured in blocks; both the default and maximum are infinity.
3. Show Indicator: whether to display a moving arrow indicator along the path to indicate the direction. Enabled by default.

视频：[哔哩哔哩](https://www.bilibili.com/video/BV1vwvvBrEs7/)

本模组添加了一个新方块：寻矿器  
使用 9 种矿物在工作台合成，摆放在地面上，接上按钮或其他电路元件，每当收到高电平信号时，会进行一次扫描  
它只会沿着地面、墙面寻找暴露在空气中的目标，无法寻找到隐藏在岩石中，和无法抵达的目标  
成功寻找到目标后，它将沿着地面绘制一条亮黄色的细线，通往目的地（退出存档会清除细线）
你可以通过“编辑”来调整它的设置

你可以在设置对话框的左半边选择要扫描的目标矿物，或其他任何目标方块，以及上次睡觉地点和上次死亡地点    
顶部两个按钮可以调整方块选择器的排序，或者按名称、类别来筛选方块  
底部五个按钮分别是：

1. 选中/取消选中（或者直接双击方块）
2. 收藏方块
3. 查看方块的详细信息
4. 手动输入方块 ID
5. 按列表查看已选中的所有方块和它的 ID

对话框的右半边有三个设置项：

1. 最大结果数：最多扫描多少组的目标方块，路径会把所有结果串起来。默认 1，最多 64
2. 扫描范围：单位格，默认和最大值均为无限
3. 显示箭头：是否在路径上显示一个移动的箭头，用于指示方向。默认开启

![Dialog 对话框](https://github.com/XiaofengdiZhu/MineralPathfinderForSC/blob/main/DocRes/Dialog.webp?raw=true)

![Crafting Recipe 合成表](https://github.com/XiaofengdiZhu/MineralPathfinderForSC/blob/main/DocRes/CraftingRecipe.webp?raw=true)

## Q&A 常见问题

> Q: Why it can't find the target block nearby?

A：The most probable reason is that the target block has unique data, but the data of the nearby block is different from the target one.  
For example: the data of a pulling piston block, that hasn't been placed on the ground, is 450. But once the pulling piston placed on the ground, its udata varies depending on its direction.  
There are two solutions to solve this problem:

1. Manually add all possible unique data for the pulling piston.
2. Select the block with data 0. Then it will ignore the data of the target block when scanning.

> 问：为什么目标方块就在附近却扫描不到？

答：最有可能的原因是目标方块有特殊值，而附近那个方块的特殊值与目标方块的不同  
举个例子：没有摆放到地面的粘性活塞的特殊值是 450，而它一旦摆放到地面，就会根据摆向而有不同的特殊值  
有两个解决方法：

1. 手动添加粘性活塞所有可能的特殊值
2. 选择特殊值为 0 的该方块，从而在扫描时忽略特殊值

## Icon 图标

> By me, Gemini and ChatGPT  
> 由我、Gemini、ChatGPT 联合创作

![Icon 图标](https://github.com/XiaofengdiZhu/MineralPathfinderForSC/blob/main/DocRes/OriginalIcon.webp?raw=true)