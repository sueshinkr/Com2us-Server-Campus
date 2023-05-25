# Com2us Server Campus

컴투스 서버 캠퍼스 1기(23.04.18 ~ 23.05.23)의 과제로 주어진 Web API 기반의 게임 서버를 구축해보는 프로젝트    
간단한 모바일 수집형 게임의 형태를 기반으로 컨텐츠들을 구현해보는 것이 목표    
ASP.NET Core와 C#을 활용하였으며, MySQL과 Redis를 사용    

***

## 구현 항목
* [계정 생성](#계정-생성)
* [로그인 + 공지](#로그인)
* [우편](#우편)
* [출석](#출석)
* [인앱결제](#인앱결제)
* [강화](#강화)
* [던전 스테이지](#던전-스테이지)
* [채팅](#채팅)

***
## 구현 공통사항

컨텐츠 구현에 필요한 기능들을 개별 컨트롤러로 구분해서 사용    
DI 패턴을 활용하였으며, 사용되는 서비스별로 생명주기를 다르게 하여 관리    
MySQL은 SQLKata 라이브러리를 활용 [깃허브 링크](https://github.com/sqlkata/querybuilder)    
Redis는 CloudStructures 라이브러리를 활용 [깃허브 링크](https://github.com/xin9le/CloudStructures)    
로그는 ZLogger 라이브러리를 활용 [깃허브 링크](https://github.com/Cysharp/ZLogger)    
유니크 ID값 생성에는 트위터의 Snowflake와 유사한 로직을 가지는 IdGen 라이브러리를 사용 [깃허브 링크](https://github.com/RobThree/IdGen)    

Redis는 인메모리 캐시 DB로 활용    
MySQL의 DB는 AccountDB / GameDB / MasterDB로 구성
* AccountDB : 계정 데이터 저장
* MasterDB : 게임의 기본 데이터를 마스터데이터로 저장, 서버 실행 후 변경되지 않는다고 가정
* GameDB : 유저 정보를 비롯한 게임의 전반적인 데이터들을 저장

컨텐츠 로직에 가장 많이 관여하는 GameDB의 테이블은 다음과 같이 구성되어있음
![ERD](https://github.com/sueshinkr/Com2us-Server-Campus/assets/100945798/bc84ceaa-6763-4493-84c1-a81161fbfac3)


미들웨어를 사용하여 계정생성, 로그인 이외의 요청시에는 로그인시 발급받은 인증토큰을 확인    
또한 같은 유저의 요청이 동시에 처리될 수 없도록 락 설정

## 구현 항목 상세

### 계정 생성
* 아이디 중복시 실패
* 비밀번호는 보안을 위해 salting 및 SHA256으로 해싱하여 저장
* AccountDB에 계정 데이터 추가
* 계정마다 존재하는 유저 데이터를 생성하여 GameDB에 추가

### 로그인 
* 아이디, 비밀번호 검증
* 게임 버전 데이터(앱 버전, 마스터데이터 버전) 검증
* 인증토큰을 생성하여 redis에 저장
* 유저의 게임 데이터를 로딩
* 공지 로딩 (공지 데이터는 편의를 위해 redis에 저장되어있다고 가정)
* 채팅로비 접속
	* 100명의 정원을 가진 1-100번 로비가 존재
	* 현재 인원수가 정원의 70퍼센트 이하인 로비 중 가장 인원이 많은 순으로 우선 배정
	* 모든 로비의 정원이 70퍼센트를 넘었을 경우 인원이 적은 순으로 배정

### 우편
우편함 열기 / 우편 읽기 / 우편 아이템 수령 / 우편 삭제 기능 구현
* 우편함의 페이지가 존재한다고 가정하여 한개 페이지마다 최대 20개의 우편이 표시되도록 설정
* 하나의 우편에는 한개 또는 다수의 아이템이 포함되어있거나 포함되지 않을 수 있음
* 우편의 읽음 여부와 아이템 수령 여부가 표시되도록 설정
* 우편의 유효 기간이 존재
* 우편 삭제시에는 물리적 삭제가 아닌 논리적 삭제 처리

### 출석
출석부 열기 / 새로운 출석 처리 기능 구현
* 연속으로 출석하지 않았거나 연속 30일 출석이 넘었을 경우 1일부터 새로 시작
* 출석 보상은 우편함을 통해 지급

### 인앱결제
* 스토어를 통한 인앱결제 정보가 주어진다고 가정
* 중복된 결제인지 검증
* 인앱상품은 패키지 형태로 존재, 패키지에 다수의 아이템이 포함되어있을 수 있음
* 아이템은 우편함을 통해 지급

### 강화
* 강화 최대 횟수 제한이 존재하는 아이템만 강화 가능
* 강화 실패시 파괴, 논리적 삭제 처리

### 던전 스테이지
스테이지 리스트 열기 / 스테이지 진입 / 아이템 획득 / 적 처치 / 스테이지 완료 처리 기능 구현
* 스테이지는 순차별 진입만 가능
* 각 스테이지마다 마스터데이터에 정해져있는 아이템 / 적 정보가 존재
* 클라이언트에서 아이템 획득시 / 적 처치시마다 요청을 보낸다고 가정
* 획득한 아이템은 redis에 임시 저장, 스테이지 완료시 유저에게 지급
* 스테이지 완료 조건은 해당 스테이지의 모든 적 처치
* 스테이지 완료시 유저에게 경험치 지급, 스테이지 완료 정보 갱신(클리어 랭크, 시간)

### 채팅
채팅 로비 지정 진입 / 채팅 송신 / 채팅 수신 기능 구현
* redis만을 사용하여 구현
* 채팅 로비 목록은 SortedSet을 사용, Score를 현재 접속중인 인원수로 설정하여 정렬
* 각 유저별 접속중인 로비 목록은 Hash를 사용
* 채팅 내역은 Stream을 사용
* 채팅 로비 접속시 동시 접근을 막기 위해 luaScript를 사용