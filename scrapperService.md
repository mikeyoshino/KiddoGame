kiddo คือเว็บรวมเกมส์ ้html 5 มาใหม่ที่นี้มีหลายหมวดหมู่ซึ่งเกมส์ดึงมาจาก https://gamedistribution.com/games/ โดยถ้า inspect หน้าเว็บจะมี iframe อยู่ตามนี้  <iframe src="https://html5.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2/?gd_sdk_referrer_url=https://gamedistribution.com/games/67-clicker/" width="100%" height="680" allowfullscreen=""></iframe> แต่เวลากดเล่นมันจะไปดึงหน้าเกมส์จริงๆ เป็น https://html5.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2/?utm_source=gamedistribution.com&utm_medium=67-clicker&utm_campaign=block-and-redirect ซึ่งดูจาก pattern เราแค่เอาf078134f39634ca78dcd4a8479a314a2 จาก iframe อันแรกมาแปะที่ลิงค์ที่สองก็จะได้หน้าเกมส์ ซึ่งเราก็เอาหน้าเกมส์นี้ใส่ iframe และแสดงผลบนเว็บอีกครั้ง 

ซึ่งสิ่งที่ต้องมี
- scrapper service project โดยใช้ python ซึ่งสิ่งที่มันจะทำคือไปที่หน้าเว็บ https://gamedistribution.com/games/ และเอาข้อมูลเช่น 
  game title
  madeby (Published by:)
  Language (can be list of language game support)
  Gender
  Age Group

  DESCRIPTION
  INSTRUCTIONS
  Genres
  Tags
  THUMBNAILS & ICONS
thery have 3 thumbnail like this 
<div class="games_gameThumnailImage__eM2Tb grid gap-4"><div class="one"><img alt="f078134f39634ca78dcd4a8479a314a2-512x512.jpg" src="https://img.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2-512x512.jpg"></div><div class="two"><img alt="f078134f39634ca78dcd4a8479a314a2-512x384.jpg" src="https://img.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2-512x384.jpg"></div><div class="three"><img alt="f078134f39634ca78dcd4a8479a314a2-200x120.jpg" src="https://img.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2-200x120.jpg"></div></div>

so select one.

สิ่งที่ scrapper ทำ
 - มีแค่คำสั่งเดียวและ skip record ที่เราทำการ scrappr มาเเล้วโดยการ skip จะต้องเร็วเช่นเรา scrape มาเเล้ว 1000 โพส เเล้วเราหยุดวันต่อมาทำต่อ การโค๊ตจะไล่ 1000 มันช้ามากต้องหาวีธีที่ได้ผลในการเริ่มต่อทันที่
 ซึ่งในเว็บ https://gamedistribution.com/games/ ทำ pagination ไว้ และเวลากดเปลี่ยนหน้ามันยิง post request แบบนี้ ดูประเภทเป็น graph 

 curl.exe ^"https://gd-website-api.gamedistribution.com/graphql^" ^
  -X POST ^
  -H ^"User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0^" ^
  -H ^"Accept: */*^" ^
  -H ^"Accept-Language: en-US,en;q=0.9^" ^
  -H ^"Accept-Encoding: gzip, deflate, br, zstd^" ^
  -H ^"Referer: https://gamedistribution.com/^" ^
  -H ^"content-type: application/json^" ^
  -H ^"apollographql-client-name: GDWebSite^" ^
  -H ^"apollographql-client-version: 1.0^" ^
  -H ^"authorization: ^" ^
  -H ^"Origin: https://gamedistribution.com^" ^
  -H ^"Connection: keep-alive^" ^
  -H ^"Sec-Fetch-Dest: empty^" ^
  -H ^"Sec-Fetch-Mode: cors^" ^
  -H ^"Sec-Fetch-Site: same-site^" ^
  -H ^"Priority: u=0^" ^
  -H ^"TE: trailers^" ^
  --data-raw ^"^{^\^"operationName^\^":^\^"GetGamesSearched^\^",^\^"variables^\^":^{^\^"id^\^":^\^"^\^",^\^"perPage^\^":30,^\^"page^\^":1,^\^"search^\^":^\^"^\^",^\^"UIfilter^\^":^{^},^\^"filters^\^":^{^}^},^\^"query^\^":^\^"fragment CoreGame on SearchHit ^{^\^\n  objectID^\^\n  title^\^\n  company^\^\n  visible^\^\n  exclusiveGame^\^\n  slugs ^{^\^\n    name^\^\n    __typename^\^\n  ^}^\^\n  assets ^{^\^\n    name^\^\n    __typename^\^\n  ^}^\^\n  firstActiveDate^\^\n  lastPublishedAt^\^\n  sortScore^\^\n  __typename^\^\n^}^\^\n^\^\nquery GetGamesSearched(^$id: String^! = ^\^\^\^"^\^\^\^", ^$perPage: Int^! = 0, ^$page: Int^! = 0, ^$search: String^! = ^\^\^\^"^\^\^\^", ^$UIfilter: UIFilterInput^! = ^{^}, ^$filters: GameSearchFiltersFlat^! = ^{^}, ^$sortBy: KnownOrder, ^$sortByGeneric: ^[String^!^], ^$sortByCountryPerf: SortByCountryPerf^! = ^{^}, ^$sortByGenericWithDirection: ^[SortByGenericWithDirection^!^], ^$sortByScore: SortByScore) ^{^\^\n  gamesSearched(^\^\n    input: ^{collectionObjectId: ^$id, hitsPerPage: ^$perPage, page: ^$page, search: ^$search, UIfilter: ^$UIfilter, filters: ^$filters, sortBy: ^$sortBy, sortByCountryPerf: ^$sortByCountryPerf, sortByGeneric: ^$sortByGeneric, sortByGenericWithDirection: ^$sortByGenericWithDirection, sortByScore: ^$sortByScore^}^\^\n  ) ^{^\^\n    hitsPerPage^\^\n    nbHits^\^\n    nbPages^\^\n    page^\^\n    hits ^{^\^\n      ...CoreGame^\^\n      __typename^\^\n    ^}^\^\n    filters ^{^\^\n      title^\^\n      key^\^\n      type^\^\n      values^\^\n      __typename^\^\n    ^}^\^\n    __typename^\^\n  ^}^\^\n^}^\^"^}^"

  และ response บางส่วนถูกตัดออกไปเพราะมันยาว ได้ประมานี้
  {"data":{"gamesSearched":{"hitsPerPage":30,"nbHits":21344,"nbPages":712,"page":1,"hits":[{"objectID":"de35402342e2480f824b75e44f7ac5ba","title":"Hard Puzzle","company":"Playgama","visible":true,"exclusiveGame":0,"slugs":[{"name":"hard-puzzle","__typename":"SlugType"}],"assets":[{"name":"de35402342e2480f824b75e44f7ac5ba-512x384.jpg","__typename":"AssetType"},{"name":"de35402342e2480f824b75e44f7ac5ba-512x512.jpg","__typename":"AssetType"},{"name":"de35402342e2480f824b75e44f7ac5ba-200x120.jpg","__typename":"AssetType"},{"name":"de35402342e2480f824b75e44f7ac5ba-1280x720.jpg","__typename":"AssetType"},{"name":"de35402342e2480f824b75e44f7ac5ba-1280x550.jpg","__typename":"AssetType"}],"firstActiveDate":"2026-04-15T07:52:53.148Z","lastPublishedAt":"2026-04-15T07:52:53.158Z","sortScore":null,"__typename":"SearchHit"},{"objectID":"d50d0bb0164f460891748b01ff084b0b","title":"World Soccer","company":"MOVISOFT.Co.,Ltd","visible":true,"exclusiveGame":0,"slugs":[{"name":"world-soccer","__typename":"SlugType"}],"assets":[{"name":"d50d0bb0164f460891748b01ff084b0b-1280x550.jpg","__typename":"AssetType"},{"name":"d50d0bb0164f460891748b01ff084b0b-200x120.jpg","__typename":"AssetType"},


  ออกแบบโดยใช้ supabase database 
  ออกแบบให้มี slug โดยใช้ชื่อเกมส์ - เพราะว่าเราจะไว้แสเดงผลในเว็ฐเราที่ทำเอง
  ควรมีข้อมูลเช่น วันที่สร้าง จำนวนคนดูหรือเล่น 
  ขอสร้าง folder สำหรับ service ส่วนเว็บค่อยทำตามที่หลัง
 


