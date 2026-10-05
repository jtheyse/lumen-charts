param([string]$BaseUrl='http://localhost:5188')
$ErrorActionPreference='Stop'
$checks=0
function Verify($condition,[string]$message){if(-not $condition){throw $message};$script:checks++;Write-Output "PASS $message"}
foreach($path in @('/health','/_framework/blazor.web.js','/_content/Lumen.Charts.Blazor/lumen.css','/_content/Lumen.Charts.Blazor/lumen.js')){
 $r=Invoke-WebRequest "$BaseUrl$path" -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200) "Asset/health $path"
}
$r=Invoke-WebRequest "$BaseUrl/sports" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,'class="lumen-chart lumen-fit"')).Count -eq 28 -and $r.Content.Contains('id="lap-heart"') -and $r.Content.Contains('id="best-efforts"') -and $r.Content.Contains('id="zone-strip"') -and $r.Content.Contains("class='lumen-strip-key'") -and $r.Content.Contains('id="scores"') -and ([regex]::Matches($r.Content,"class='lumen-bar-track'")).Count -eq 3 -and $r.Content.Contains('id="ride-channels"') -and $r.Content.Contains('id="season-arc"') -and $r.Content.Contains('id="gap"') -and ([regex]::Matches($r.Content,"class='lumen-end'")).Count -eq 8 -and $r.Content.Contains('not real training data') -and $r.Content.Contains('id="hypnogram"') -and $r.Content.Contains("class='lumen-span'") -and $r.Content.Contains('id="training-calendar"') -and $r.Content.Contains("class='lumen-day'") -and $r.Content.Contains('id="laps"') -and $r.Content.Contains('id="next-session"') -and $r.Content.Contains('id="field"') -and ([regex]::Matches($r.Content,"class='lumen-block'")).Count -eq 31) 'The Sports & performance page answers 200 and prerenders its twenty-eight charts, each set to fit its card, the laps'' heart rates and the best efforts, the time-in-zone strip with its key, the three session scores on their tracks, the season arc, the gap to the leader with its eight end labels, the ride channels, last night''s sleep stages, the training calendar, the run''s four laps, the next session''s ten steps and the seventeen bins of how the field finished among them'
$r=Invoke-WebRequest "$BaseUrl/" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('class="lumen-chart lumen-fit"') -and $r.Content.Contains('<b>23</b><span>Chart types</span>') -and $r.Content.Contains('>Calendar</button>') -and $r.Content.Contains('>Blocks</button>') -and $r.Content.Contains('>Strip</button>')) 'The home page answers 200, its chart explorer set to fit its card, with twenty-three chart types and a calendar, blocks and a strip among them'
Verify (([regex]::Matches($r.Content,'class="lumen-chart lumen-fit"')).Count -eq 6 -and $r.Content.Contains("data-node='sources'") -and $r.Content.Contains("viewBox='0 0 900 460'")) 'The home page prerenders its network graph set to fit its card too, drawn at its own width until the browser measures the card, and the four charts that fit a card'
$types=Invoke-RestMethod "$BaseUrl/api/charts/types"
Verify ($types.Count -eq 23 -and $types -contains 'Gauge' -and $types -contains 'Ring' -and $types -contains 'Timeline' -and $types -contains 'Range' -and $types -contains 'Calendar' -and $types -contains 'Blocks' -and $types -contains 'Strip') 'Twenty-three chart types, gauge, ring, timeline, range, calendar, blocks and strip among them'
foreach($kind in @('Line','Area','Scatter','Bubble','Column','Bar','StackedColumn','Donut','Heatmap','Radar')){
 $spec=@{title='API test';kind=$kind;series=@(@{name='Sample';points=@(@{x=0;y=2;label='A'},@{x=1;y=4;label='B'},@{x=2;y=3;label='C'})})}
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body ($spec|ConvertTo-Json -Depth 10) -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200 -and $r.Headers['Content-Type'] -like 'image/svg+xml*') "$kind SVG response"
 $xml=[xml]$r.Content
 Verify ($xml.DocumentElement.LocalName -eq 'svg') "$kind XML validity"
}
$payload='{"title":"CSV","kind":"Line","series":[{"name":"Source","points":[{"x":0,"y":1},{"x":1,"y":null}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $payload
Verify ($r.Content.Contains('Series,X,Y,Label,Size') -and $r.Content.Contains('"Source",1,,')) 'CSV retains missing observation'
$families=@(
 @{name='Candlestick';marker="rx='1'";body='{"title":"Prices","kind":"Candlestick","series":[{"name":"ACME","points":[{"x":0,"open":10,"high":12,"low":9,"close":11},{"x":1,"open":11,"high":13,"low":10,"close":10.4}]}]}'},
 @{name='Ohlc';marker="x1='59'";body='{"title":"Prices","kind":"Ohlc","series":[{"name":"ACME","points":[{"x":0,"open":10,"high":12,"low":9,"close":11},{"x":1,"open":11,"high":13,"low":10,"close":10.4}]}]}'},
 @{name='Band';marker="fill-opacity='.16'";body='{"title":"Forecast","kind":"Band","series":[{"name":"Demand","points":[{"x":0,"y":10,"low":8,"high":12},{"x":1,"y":12,"low":9,"high":15}]}]}'},
 @{name='Histogram';marker='equal-width bins';body='{"title":"Latency","kind":"Histogram","bins":5,"series":[{"name":"Requests","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":2},{"x":3,"y":3},{"x":4,"y":5},{"x":5,"y":8}]}]}'},
 @{name='Box';marker='median';body='{"title":"Spread","kind":"Box","series":[{"name":"Europe","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":3},{"x":3,"y":4},{"x":4,"y":40}]}]}'},
 @{name='Violin';marker='observations, median';body='{"title":"Shape","kind":"Violin","series":[{"name":"Europe","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":2},{"x":3,"y":3},{"x":4,"y":5},{"x":5,"y":8},{"x":6,"y":13},{"x":7,"y":21}]}]}'},
 @{name='Gauge';marker="class='lumen-gauge-value'";body='{"title":"Recovery","kind":"Gauge","yLabel":"%","gaugeSweep":270,"yZones":{"zones":[{"name":"Low","upper":33,"color":"#DD4B45"},{"name":"Moderate","upper":66,"color":"#A88200"},{"name":"Good","upper":"Infinity","color":"#2E9B58"}]},"annotations":[{"axis":"Y","from":60,"label":"Average"}],"series":[{"name":"Recovery","points":[{"x":0,"y":72,"label":"Recovery"}]}]}'},
 @{name='Ring';marker="class='lumen-ring-progress'";body='{"title":"Activity","kind":"Ring","series":[{"name":"Move","goal":600,"points":[{"x":0,"y":540,"label":"kcal"}]},{"name":"Exercise","goal":30,"points":[{"x":0,"y":47,"label":"min"}]},{"name":"Stand","goal":12,"points":[{"x":0,"y":9,"label":"h"}]}]}'},
 @{name='Timeline';marker="class='lumen-span'";body='{"title":"Night","kind":"Timeline","xFormat":"TimeOfDay","series":[{"name":"Light","points":[{"x":82800,"xEnd":84600}]},{"name":"REM","points":[{"x":84600,"xEnd":86220}]}]}'},
 @{name='Range';marker="class='lumen-range'";body='{"title":"Heart rate","kind":"Range","xAxis":"Time","series":[{"name":"Heart rate","points":[{"x":1789171200000,"y":74,"low":52,"high":168,"label":"12 Sep"},{"x":1789257600000,"y":70,"low":48,"high":150,"label":"13 Sep"},{"x":1789344000000,"low":50,"high":140,"label":"14 Sep"}]}]}'},
 @{name='Calendar';marker="class='lumen-day'";body='{"title":"Training","kind":"Calendar","xAxis":"Time","timeZone":"America/New_York","yZones":{"zones":[{"name":"Easy","upper":50},{"name":"Hard","upper":"Infinity"}]},"annotations":[{"axis":"X","from":1789387200000,"label":"Race"}],"series":[{"name":"Stress","points":[{"x":1789387200000,"y":40,"label":"Ride"},{"x":1789390800000,"y":30},{"x":1789473600000,"y":0},{"x":1789560000000,"y":20}]}]}'},
 @{name='Blocks';marker="class='lumen-block'";body='{"title":"Workout","kind":"Blocks","xFormat":"Duration","includeZero":true,"yMax":300,"series":[{"name":"Plan","zones":{"zones":[{"name":"Easy","upper":187.5},{"name":"Tempo","upper":225},{"name":"Threshold","upper":"Infinity"}]},"points":[{"x":0,"xEnd":600,"y":150,"label":"Warm-up"},{"x":600,"xEnd":1080,"y":250,"label":"Interval 1"},{"x":1080,"xEnd":1320,"y":125,"label":"Recovery"}]},{"name":"Power","kind":"Line","points":[{"x":0,"y":118},{"x":660,"y":252},{"x":1200,"y":130}]}]}'})
foreach($family in $families){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $family.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') "$($family.name) SVG response"
 Verify ($r.Content.Contains($family.marker)) "$($family.name) rendered its own geometry"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $families[0].body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Open,High,Low,Close')) 'Candlestick CSV exports prices'
# A gauge's score and a ring's progress are named with their zone, unit and goal, a gauge's target is a tick across its arc, and a
# ring past its goal runs on over itself, its leading end shadowed.
$gauge=$families|Where-Object{$_.name -eq 'Gauge'};$ring=$families|Where-Object{$_.name -eq 'Ring'}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $gauge.body
Verify ($r.Content.Contains("aria-label='Recovery: 72 %, Good'") -and $r.Content.Contains("aria-label='Good: above 66'") -and $r.Content.Contains("class='lumen-gauge-target'") -and $r.Content.Contains('>Average: 60<')) 'A gauge posted as JSON names its score and zone and marks its target'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $ring.body
Verify ($r.Content.Contains("aria-label='Move: 540 of 600 kcal, 90 %'") -and $r.Content.Contains("aria-label='Exercise: 47 of 30 min, 157 %'") -and ([regex]::Matches($r.Content,"class='lumen-ring-shadow'")).Count -eq 3 -and $r.Content.Contains('>Stand: 9 of 12 h<')) 'Rings posted as JSON name each value and goal, and only the ring past its goal casts a shadow'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $ring.body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Goal') -and $r.Content.Contains('"Move",0,540,"kcal",1,600')) 'Ring CSV carries each ring''s goal'
foreach($bad in @(@{body=$gauge.body.Replace('{"x":0,"y":72,"label":"Recovery"}','{"x":0,"y":72},{"x":1,"y":40}');reason='exactly one point';name='A gauge of two points'},
  @{body=$gauge.body.Replace('"gaugeSweep":270','"gaugeSweep":120');reason='between 180';name='A gauge sweep of 120 degrees'},
  @{body=$ring.body.Replace('"goal":600','"goal":0');reason='positive';name='A ring goal of zero'},
  @{body=$ring.body.Replace('"kind":"Ring",','"kind":"Ring","yZones":{"zones":[{"name":"All","upper":"Infinity"}]},');reason='no zones';name='Zones on rings'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# A timeline names each span with its state, times and length, totals each state in its legend and joins the stages where one
# ends as the next begins; a range bar names its day, its ends and its average, and a time-of-day axis reads the clock.
$timeline=$families|Where-Object{$_.name -eq 'Timeline'};$range=$families|Where-Object{$_.name -eq 'Range'}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $timeline.body
Verify ($r.Content.Contains("aria-label='REM: 23:30 to 23:57, 27 min'") -and $r.Content.Contains('>Light 0:30, 53 %<') -and $r.Content.Contains('>REM 0:27, 47 %<') -and ([regex]::Matches($r.Content,"class='lumen-connectors'")).Count -eq 1 -and $r.Content.Contains('>23:30<')) 'A timeline posted as JSON names its spans, totals its states and joins them'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $timeline.body.Replace('"kind":"Timeline",','"kind":"Timeline","timelineConnectors":false,')
Verify ($r.StatusCode -eq 200 -and -not $r.Content.Contains('lumen-connectors') -and ([regex]::Matches($r.Content,"class='lumen-span'")).Count -eq 2) 'A timeline without connectors draws its spans alone'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $timeline.body
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,XEnd') -and $r.Content.Contains('"Light",82800,,"",1,84600')) 'Timeline CSV carries where each span ends'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $range.body
Verify ($r.Content.Contains("aria-label='12 Sep: 52 to 168, average 74'") -and $r.Content.Contains("aria-label='14 Sep: 50 to 140'") -and ([regex]::Matches($r.Content,"<circle cx=")).Count -ge 2) 'Range bars posted as JSON name their ends and averages, with a dot for each average'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $range.body
Verify ($r.Content.StartsWith('Series,X,XTime,Y,Label,Size,Low,High') -and $r.Content.Contains('"Heart rate",1789344000000,2026-09-14T00:00:00.000Z,,"14 Sep",1,50,140')) 'Range CSV carries each bar''s bounds'
$sleep='{"title":"Sleep timing","kind":"Range","yFormat":"TimeOfDay","yReversed":true,"series":[{"name":"Sleep","points":[{"x":0,"low":82800,"high":109800,"label":"Mon"},{"x":1,"low":84600,"high":111600,"label":"Tue"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $sleep
$early=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>00:00<").Groups[1].Value;$late=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>06:00<").Groups[1].Value
Verify ($r.StatusCode -eq 200 -and $early -gt 0 -and $early -lt $late -and $r.Content.Contains("aria-label='Mon: 23:00 to 06:30'")) 'Sleep timing on a reversed time-of-day axis reads the clock past midnight, earlier at the top'
$beside='{"title":"Heart rate","kind":"Line","series":[{"name":"Resting","points":[{"x":0,"y":52},{"x":1,"y":48}]},{"name":"Range","kind":"Range","points":[{"x":0,"y":74,"low":52,"high":168},{"x":1,"y":70,"low":48,"high":150}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $beside
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,"class='lumen-range'")).Count -eq 2 -and $r.Content.Contains("aria-label='Resting: 1, 48'")) 'A range series beside a line posted as JSON draws both'
foreach($bad in @(@{body='{"kind":"Timeline","series":[{"name":"Light","points":[{"x":0,"xEnd":10},{"x":5,"xEnd":15}]}]}';reason='cannot overlap';name='Overlapping spans in one lane'},
  @{body=$timeline.body.Replace('"xEnd":84600','"xEnd":82800');reason='above its X';name='A span that ends where it starts'},
  @{body='{"kind":"Line","series":[{"name":"S","points":[{"x":0,"y":1,"xEnd":2}]}]}';reason='series drawn as blocks';name='XEnd on a line chart'},
  @{body='{"kind":"Line","timelineConnectors":false,"series":[{"name":"S","points":[{"x":0,"y":1}]}]}';reason='only a timeline';name='Connectors turned off on a line chart'},
  @{body=$timeline.body.Replace('"kind":"Timeline",','"kind":"Timeline","annotations":[{"axis":"Y","from":1}],');reason='X annotations';name='A Y annotation on a timeline'},
  @{body=$timeline.body.Replace('{"name":"REM",','{"name":"REM","secondary":true,');reason='secondary axis';name='A secondary series on a timeline'},
  @{body=$range.body.Replace('"name":"Heart rate","points"','"name":"Heart rate","zones":{"zones":[{"name":"All","upper":"Infinity"}]},"points"');reason='takes no zones';name='Zones on a range series'},
  @{body=$range.body.Replace('"y":74,','"y":200,');reason='between its Low and its High';name='A range average outside its bar'},
  @{body='{"kind":"Line","yAxis":"Log","yFormat":"TimeOfDay","series":[{"name":"S","points":[{"x":0,"y":1}]}]}';reason='time-of-day';name='A time-of-day format on a log axis'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# A calendar counts its days in its zone and adds up each day's points, names each day with its labels, total and zone, outlines an
# annotated day, leaves a day without activity an empty cell, and lays a month out as bubbles; its CSV carries each point.
$calendar=$families|Where-Object{$_.name -eq 'Calendar'}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $calendar.body
Verify ($r.Content.Contains("aria-label='Mon 14 Sep 2026, Ride: 70, Hard'") -and $r.Content.Contains("aria-label='Wed 16 Sep 2026: 20, Easy'") -and $r.Content.Contains("aria-label='Race: Mon 14 Sep 2026'") -and ([regex]::Matches($r.Content,"class='lumen-outline'")).Count -eq 1 -and ([regex]::Matches($r.Content,"class='lumen-track'")).Count -eq 1 -and ([regex]::Matches($r.Content,'data-point=')).Count -eq 2 -and $r.Content.Contains('>Hard<') -and $r.Content.Contains('>Sep<')) 'A calendar posted as JSON adds up a day in its zone, names each day with its zone, outlines the race and keeps a rest day empty'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $calendar.body.Replace('"kind":"Calendar",','"kind":"Calendar","calendarLayout":"Months","calendarCell":"Bubble","weekStart":"Sunday",')
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('>September<') -and ([regex]::Matches($r.Content,"<circle [^>]*class='lumen-track'")).Count -eq 3 -and ([regex]::Matches($r.Content,"<circle [^>]*class='lumen-day'")).Count -eq 2 -and $r.Content.Contains("text-anchor='middle' class='lumen-muted' font-size='10'>S<")) 'A month of bubbles from Sunday posted as JSON draws a track for every day and a bubble for each day with activity'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $calendar.body
Verify ($r.Content.StartsWith('Series,X,XTime,Y,Label,Size') -and $r.Content.Contains('"Stress",1789387200000,2026-09-14T12:00:00.000Z,40,"Ride",1') -and $r.Content.Contains('"Stress",1789390800000,2026-09-14T13:00:00.000Z,30,"",1')) 'Calendar CSV carries each original point with its moment'
foreach($bad in @(@{body=$calendar.body.Replace('"xAxis":"Time",','');reason='time X axis';name='A calendar without a time axis'},
  @{body=$calendar.body.Replace(']}]}',']},{"name":"Again","points":[{"x":1789387200000,"y":1}]}]}');reason='one series of days';name='A calendar of two series'},
  @{body=$calendar.body.Replace('"axis":"X"','"axis":"Y"');reason='X annotations';name='A Y annotation on a calendar'},
  @{body=$calendar.body.Replace('"y":40,','"y":40,"color":"#123456",');reason='colours of their own';name='A point colour on a calendar'},
  @{body=$calendar.body.Replace('"y":20}','"y":-5}');reason='negative';name='A negative day on a calendar'},
  @{body='{"kind":"Line","calendarLayout":"Months","series":[{"name":"S","points":[{"x":0,"y":1}]}]}';reason='calendar charts only';name='A calendar layout on a line chart'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# Blocks stand on the bottom edge of their plot exactly from X to XEnd, each named with its label, span, height and zone and coloured by
# its zone, under the line beside them; laps rise on a reversed pace axis from past the slowest; a series' own kind draws them on a line
# chart; and CSV carries where each ends.
$blocks=$families|Where-Object{$_.name -eq 'Blocks'}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $blocks.body
Verify ($r.Content.Contains("aria-label='Warm-up: 0:00 to 10:00, 150, Easy'") -and $r.Content.Contains("aria-label='Interval 1: 10:00 to 18:00, 250, Threshold'") -and $r.Content.Contains("aria-label='Recovery: 18:00 to 22:00, 125, Easy'") -and ([regex]::Matches($r.Content,"class='lumen-block'")).Count -eq 3 -and $r.Content.Contains("<path class='lumen-block' d='M76,344 L76,215 A4,4 0 0 1 80,211 ") -and $r.Content.Contains("fill='#2E9B58'/></g>") -and $r.Content.IndexOf("class='lumen-block'") -lt $r.Content.IndexOf("fill='none' stroke=")) 'Blocks posted as JSON stand on the plot''s bottom edge from their start, rounded at the top, each named and coloured by its zone, under the line beside them'
$laps='{"title":"Laps","kind":"Blocks","yFormat":"Duration","yReversed":true,"annotations":[{"axis":"Y","from":280,"label":"Average"}],"series":[{"name":"Laps","points":[{"x":0,"xEnd":1,"y":300,"label":"Lap 1"},{"x":1,"xEnd":2,"y":280,"label":"Lap 2"},{"x":2,"xEnd":3,"y":260,"label":"Lap 3"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $laps
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains("aria-label='Lap 1: 0 to 1, 5:00'") -and $r.Content.Contains("<path class='lumen-block' d='M76,344 L76,303.66666667 A4,4 0 0 1 80,299.66666667 L336.16666667,299.66666667 A4,4 0 0 1 340.16666667,303.66666667 L340.16666667,344 Z'") -and $r.Content.Contains("L870,344 Z'") -and $r.Content.Contains('>Average: 4:40<')) 'Laps posted as JSON rise on a reversed pace axis from past the slowest, which stands a sixth of the plot, the hairline between neighbours, the last ending at the plot''s edge'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","series":[{"name":"Power","points":[{"x":0,"y":100},{"x":10,"y":200}]},{"name":"Plan","kind":"Blocks","points":[{"x":0,"xEnd":5,"y":120},{"x":5,"xEnd":10,"y":180}]}]}'
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,"class='lumen-block'")).Count -eq 2 -and $r.Content.Contains("aria-label='Plan: 5 to 10, 180'") -and $r.Content.Contains("aria-label='Power: 10, 200'")) 'Blocks as a series'' own kind beside a line posted as JSON draw both'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $blocks.body
Verify ($r.Content.StartsWith("Series,X,Y,Label,Size,XEnd`r`n") -and $r.Content.Contains("`"Plan`",600,250,`"Interval 1`",1,1080`r`n") -and $r.Content.Contains("`"Power`",660,252,`"`",1,`r`n")) 'Blocks CSV carries where each block ends'
foreach($bad in @(@{body='{"kind":"Blocks","series":[{"name":"Plan","points":[{"x":0,"xEnd":5,"y":1},{"x":4,"xEnd":6,"y":2}]}]}';reason='cannot overlap';name='Overlapping blocks in one series'},
  @{body='{"kind":"Blocks","series":[{"name":"Plan","points":[{"x":0,"y":1}]}]}';reason='an XEnd above it';name='A block without an end'},
  @{body='{"kind":"Blocks","series":[{"name":"Plan","points":[{"x":0,"xEnd":1}]}]}';reason='cannot be missing';name='A block without a height'},
  @{body='{"kind":"Column","series":[{"name":"Plan","kind":"Blocks","points":[{"x":0,"xEnd":1,"y":1}]}]}';reason='no slot';name='Blocks on a column chart'},
  @{body='{"kind":"Blocks","xAxis":"Log","series":[{"name":"Plan","points":[{"x":1,"xEnd":2,"y":1}]}]}';reason='linear or a time X axis';name='Blocks on a logarithmic X axis'},
  @{body='{"kind":"Blocks","series":[{"name":"Plan","trend":true,"points":[{"x":0,"xEnd":1,"y":1},{"x":1,"xEnd":2,"y":2}]}]}';reason='no trend line';name='A trend through blocks'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
$summary='{"title":"Warehouse","kind":"Box","series":[{"name":"Asia","points":[],"summary":{"q1":205,"median":228,"q3":252,"lowerWhisker":160,"upperWhisker":318,"outliers":[352,371]}}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'A precomputed box summary posted as JSON renders'
Verify ($r.Content.Contains('Asia (supplied summary): median 228, quartiles 205 to 252, whiskers 160 to 318, 2 outliers') -and $r.Content.Contains('>Asia (summary)<') -and -not $r.Content.Contains('data-point=')) 'A supplied summary is drawn as given and names no point'
foreach($kind in @('Violin','Line')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary.Replace('"kind":"Box"','"kind":"'+$kind+'"') -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) "A summary on a $kind chart is rejected"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $summary.Replace('"points":[]','"points":[{"x":0,"y":230}]') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A series with both points and a summary is rejected'
$compared='{"title":"Latency","kind":"Histogram","bins":4,"series":[{"name":"Before","points":[{"x":0,"y":1},{"x":1,"y":5},{"x":2,"y":12},{"x":3,"y":25}]},{"name":"After","points":[{"x":0,"y":2},{"x":1,"y":3},{"x":2,"y":4},{"x":3,"y":39}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $compared -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'A two-series histogram renders'
Verify ($r.Content.Contains('Before, 1 to 10.5: 2 observations') -and $r.Content.Contains('After, 1 to 10.5: 3 observations') -and $r.Content.Contains('>Before<') -and $r.Content.Contains('>After<') -and $r.Content.Contains('8 observations across 2 series in 4 shared equal-width bins')) 'A two-series histogram draws and names both series'
$five=$compared.Replace('"series":[','"series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","points":[{"x":0,"y":1}]},{"name":"C","points":[{"x":0,"y":1}]},')
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $five -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A histogram of five series is rejected'
foreach($bad in @('{"kind":"Candlestick","series":[{"name":"P","points":[{"x":0,"y":1}]}]}','{"kind":"Band","series":[{"name":"F","points":[{"x":0,"y":1,"low":5,"high":2}]}]}','{"kind":"Histogram","bins":0,"series":[{"name":"H","points":[{"x":0,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid family request rejected'
}
$timeSpec='{"title":"Time","kind":"Line","xAxis":"Time","series":[{"name":"Signal","points":[{"x":1767225600000,"y":3},{"x":1769904000000,"y":5},{"x":1772323200000,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content -match 'Feb 2026|Jan 2026') 'Time axis renders calendar labels'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $timeSpec
Verify ($r.Content.Contains('Series,X,XTime,Y,Label,Size') -and $r.Content.Contains('2026-01-01T00:00:00.000Z')) 'Time CSV adds ISO timestamps'
$zoned='{"title":"Local","kind":"Line","xAxis":"Time","timeZone":"America/New_York","series":[{"name":"Signal","points":[{"x":1767571200000,"y":1},{"x":1767592800000,"y":2},{"x":1767614400000,"y":3}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned
Verify ($r.Content.Contains('4 Jan 2026 19:00')) 'A time axis reads its calendar in the requested zone'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","xAxis":"Time","timeZone":"Mars/Olympus","series":[{"name":"S","points":[{"x":1767571200000,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An unknown time zone is rejected'
$trading='{"title":"Trading","kind":"Line","xAxis":"Time","skipWeekends":true,"series":[{"name":"Close","points":[{"x":1767571200000,"y":1},{"x":1767657600000,"y":2},{"x":1767744000000,"y":3},{"x":1767830400000,"y":4},{"x":1767916800000,"y":5},{"x":1768176000000,"y":6},{"x":1768262400000,"y":7},{"x":1768348800000,"y":8},{"x":1768435200000,"y":9},{"x":1768521600000,"y":10}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trading
Verify ($r.Content.Contains('>5 Jan<') -and -not $r.Content.Contains('>10 Jan<') -and -not $r.Content.Contains('>11 Jan<')) 'A trading axis labels no weekend'
$holiday=$trading.Replace('"skipWeekends":true','"skipWeekends":true,"timeSkips":[{"from":1767744000000,"to":1767830400000}]')
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $holiday
Verify (-not $r.Content.Contains('>7 Jan<')) 'A skipped holiday leaves the axis'
foreach($bad in @('{"kind":"Line","skipWeekends":true,"series":[{"name":"S","points":[{"x":1,"y":1}]}]}','{"kind":"Line","xAxis":"Time","timeSkips":[{"from":1767744000000,"to":1767744000000}],"series":[{"name":"S","points":[{"x":1767571200000,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'An impossible skipped span is rejected'
}
$trend='{"title":"Fit","kind":"Scatter","series":[{"name":"Accounts","trend":true,"points":[{"x":1,"y":3},{"x":2,"y":5},{"x":3,"y":7},{"x":4,"y":9}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend
Verify ($r.Content.Contains("class='lumen-trend'") -and $r.Content.Contains('R squared 1.00') -and $r.Content.Contains('rising')) 'A scatter series carries its least-squares line'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend.Replace('"trend":true','"trend":false')
Verify (-not $r.Content.Contains('lumen-trend')) 'A series that asks for no trend draws none'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $trend.Replace('"kind":"Scatter"','"kind":"Column"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A trend line on a category chart is rejected'
# 0.32.0: a moving average, a cubic and an exponential, each named for its fit; the fit is written as a string, as a graph's
# direction is, and a fit, window or degree without a trend, or out of its range, is refused with its reason.
$fits='{"title":"Fits","kind":"Scatter","yAxis":"Log","series":[{"name":"Rate","trend":true,"trendFit":"MovingAverage","trendPoints":3,"points":[{"x":0,"y":1},{"x":1,"y":3},{"x":2,"y":2},{"x":3,"y":5},{"x":4,"y":4}]},{"name":"Curve","trend":true,"trendFit":"Polynomial","trendDegree":3,"points":[{"x":0,"y":2},{"x":1,"y":3},{"x":2,"y":10},{"x":3,"y":29},{"x":4,"y":66}]},{"name":"Growth","trend":true,"trendFit":"Exponential","points":[{"x":0,"y":1},{"x":1,"y":2},{"x":2,"y":4},{"x":3,"y":8},{"x":4,"y":16}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $fits -SkipHttpErrorCheck
$growth=[regex]::Match($r.Content,"<path class='lumen-trend' d='([^']+)'[^>]*aria-label='Growth trend").Groups[1].Value -split ' '|ForEach-Object{,($_.Substring(1) -split ',' | ForEach-Object{[double]$_})}
$straight=($growth.Count -gt 100) -and (@($growth|Where-Object{[math]::Abs($growth[0][1]+($growth[-1][1]-$growth[0][1])*($_[0]-$growth[0][0])/($growth[-1][0]-$growth[0][0])-$_[1]) -gt 1e-6}).Count -eq 0)
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,"class='lumen-trend'")).Count -eq 3 -and $r.Content.Contains("aria-label='Rate trend: 3-point moving average'") -and $r.Content.Contains("aria-label='Growth trend: exponential fit, rising, R squared 1.00'") -and $straight) 'A moving average and an exponential posted as JSON draw as trends named for their fits, the exponential straight on a log axis'
Verify ($r.Content -match "aria-label='Curve trend: cubic fit, R squared [01]\.\d\d'") 'A cubic posted with its degree draws as a cubic fit'
foreach($bad in @(@{body=$fits.Replace('"trend":true,"trendFit":"MovingAverage"','"trendFit":"MovingAverage"');reason='need Trend = true';name='A moving average without a trend'},
  @{body=$fits.Replace('"trendDegree":3','"trendDegree":5');reason='2, 3 or 4';name='A fifth-degree polynomial'},
  @{body=$fits.Replace('"trendPoints":3','"trendPoints":1');reason='from 2 to 1000';name='A one-point moving average'},
  @{body=$fits.Replace('"trendFit":"Exponential"','"trendFit":"Exponential","trendPoints":5');reason='applies to TrendFit.MovingAverage';name='A window on an exponential'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $fits.Replace('"trendFit":"Exponential"','"trendFit":"Logistic"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An unknown trend fit is rejected'
# 0.33.0: a place coloured by its change and named with it, its field size a value note after its value in its label, its name and the
# CSV's Note column; a time axis that keeps its dates under long round names unless asked for the names; and each new setting refused
# where it cannot apply. The Sports & performance page prerenders its race results with their value labels.
$r=Invoke-WebRequest "$BaseUrl/sports" -SkipHttpErrorCheck
Verify ($r.Content.Contains('id="race-results"') -and ([regex]::Matches($r.Content,"class='lumen-value'")).Count -eq 10 -and $r.Content.Contains("aria-label='Position: 16-05-2026, 24/48, better than the previous'") -and $r.Content.Contains('below baseline')) 'The Sports & performance page prerenders its race results, each place and points total written and each place named with its change, and the HRV nights named with their status'
$race='{"title":"Race results","kind":"Line","xMin":-0.5,"xMax":2.5,"yReversed":true,"series":[{"name":"Position","changeColors":"LowerIsBetter","valueLabels":true,"points":[{"x":0,"y":31,"label":"11-04-2026","valueNote":"/50"},{"x":1,"y":24,"label":"16-05-2026","valueNote":"/48"},{"x":2,"y":27,"label":"04-07-2026","valueNote":"/51"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $race -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains("aria-label='Position: 16-05-2026, 24/48, better than the previous'") -and $r.Content.Contains("aria-label='Position: 04-07-2026, 27/51, worse than the previous'") -and $r.Content.Contains("aria-label='Position: 11-04-2026, 31/50'") -and $r.Content.Contains("fill='#169B8D'") -and $r.Content.Contains("fill='#D36B84'") -and ([regex]::Matches($r.Content,"class='lumen-value'")).Count -eq 3 -and $r.Content.Contains(">24<tspan class='lumen-muted' font-weight='400'>/48</tspan>")) 'A race posted as JSON colours a place better than the one before, names each change, and writes each place with its field size'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $race
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Note') -and $r.Content.Contains('"Position",1,24,"16-05-2026",1,"/48"')) 'The CSV of a race carries each field size in a Note column'
$season='{"title":"Season","kind":"Line","xAxis":"Time","timeZone":"Africa/Johannesburg","yReversed":true,"series":[{"name":"Position","points":[{"x":1775901600000,"y":31,"label":"Round 1 - Hilltop Classic"},{"x":1778925600000,"y":24,"label":"Round 2 - River Valley"},{"x":1783159200000,"y":27,"label":"Round 3 - Quarry Loop"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $season
Verify ($r.Content.Contains("aria-label='Position: Round 2 - River Valley, 24'") -and -not $r.Content.Contains('>Round') -and $r.Content -match '>\d{1,2} (Apr|May|Jun|Jul)<') 'A time axis under long round names keeps its dates, the names in the points'' tooltips'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $season.Replace('"yReversed":true','"yReversed":true,"xTicks":"PointLabels"')
Verify ($r.Content.Contains('>Round 1 - H') -and $r.Content.Contains('>Round 3 - Q')) 'XTicks PointLabels posted as JSON puts the round names under the axis, cut short'
foreach($bad in @(@{body='{"kind":"Column","series":[{"name":"S","changeColors":"LowerIsBetter","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}';reason='Change colours apply';name='Change colours on columns'},
  @{body='{"kind":"Line","series":[{"name":"S","points":[{"x":0,"y":1,"valueNote":"/123456789012345678901"}]}]}';reason='at most 20 characters';name='A value note of 22 characters'},
  @{body='{"kind":"Column","xTicks":"Axis","series":[{"name":"S","points":[{"x":0,"y":1}]}]}';reason='XTicks chooses';name='A tick source on a column chart'},
  @{body='{"kind":"Area","series":[{"name":"S","valueLabels":true,"points":[{"x":0,"y":1}]}]}';reason='Value labels apply';name='Value labels on an area'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $race.Replace('"LowerIsBetter"','"Sideways"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Unknown change colours are rejected'
$logSpec='{"title":"Log","kind":"Scatter","yAxis":"Log","series":[{"name":"Load","points":[{"x":1,"y":2},{"x":2,"y":200},{"x":3,"y":20000}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $logSpec
Verify (([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'Log axis SVG'
foreach($bad in @('{"kind":"Line","yAxis":"Log","series":[{"name":"S","points":[{"x":1,"y":0}]}]}','{"kind":"Column","xAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}','{"kind":"Line","yAxis":"Time","series":[{"name":"S","points":[{"x":1,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid axis request rejected'
}
$paired='{"title":"Two units","kind":"Line","y2Label":"Rate","series":[{"name":"Accounts","points":[{"x":0,"y":100},{"x":1,"y":500}]},{"name":"Rate","secondary":true,"points":[{"x":0,"y":2},{"x":1,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $paired
Verify ($r.Content.Contains('Rate: 1, 4') -and $r.Content.Contains('rotate(90')) 'A secondary series is measured and named on the right'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"StackedColumn","series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","secondary":true,"points":[{"x":0,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A secondary axis on a stacked chart is rejected'
$duration='{"title":"Pace","kind":"Line","yFormat":"Duration","series":[{"name":"Pace","points":[{"x":0,"y":290},{"x":1,"y":305},{"x":2,"y":320}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $duration
Verify ($r.Content.Contains('>4:50<') -and $r.Content.Contains('>5:20<') -and $r.Content.Contains('Pace: 1, 5:05')) 'A duration axis posted as JSON reads m:ss'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $duration
Verify ($r.Content.Contains('"Pace",1,305,')) 'CSV keeps durations in seconds'
$curve='{"title":"Power","kind":"Line","xAxis":"Log","xFormat":"Duration","series":[{"name":"Power","points":[{"x":1,"y":900},{"x":60,"y":420},{"x":1200,"y":290},{"x":3600,"y":260}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $curve
Verify ($r.Content.Contains('>1s<') -and $r.Content.Contains('>1m<') -and $r.Content.Contains('>1h<') -and $r.Content.Contains('Power: 20m, 290')) 'A log duration axis reads 1s, 1m and 1h'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $duration.Replace('"yFormat"','"yReversed":true,"yFormat"') -SkipHttpErrorCheck
$fast=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>4:50<").Groups[1].Value;$slow=[double][regex]::Match($r.Content,"y='([\d.]+)'[^>]*>5:20<").Groups[1].Value
Verify ($r.StatusCode -eq 200 -and $fast -gt 0 -and $fast -lt $slow) 'A reversed axis renders with the faster pace on top'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","yFormat":"Compact","series":[{"name":"Views","points":[{"x":0,"y":1500,"label":"A"},{"x":1,"y":2400000,"label":"B"}]}]}'
Verify ($r.Content.Contains('>2M<') -and $r.Content.Contains('Views: A, 1.5k')) 'Compact numbers read 1.5k and 2M'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","yReversed":true,"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
# A problem response is application/problem+json, which PowerShell hands back as bytes, so the text is read from RawContent.
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('zero baseline')) 'A reversed column chart is rejected for its zero baseline'
foreach($bad in @('{"kind":"Line","xAxis":"Time","xFormat":"Duration","series":[{"name":"S","points":[{"x":1767225600000,"y":1}]}]}','{"kind":"Line","xAxis":"Time","xFormat":"Compact","series":[{"name":"S","points":[{"x":1767225600000,"y":1}]}]}','{"kind":"Line","yFormat":"Pace","series":[{"name":"S","points":[{"x":0,"y":1}]}]}')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'A format on a time axis, or an unknown format, is rejected'
}
$annotated='{"title":"Target","kind":"Line","annotations":[{"axis":"Y","from":25,"label":"Target"},{"axis":"X","from":1,"to":2,"label":"Window"}],"series":[{"name":"S","points":[{"x":0,"y":10},{"x":1,"y":30},{"x":2,"y":20},{"x":3,"y":40}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $annotated
Verify ($r.Content.Contains('Target: 25') -and $r.Content.Contains('Window: 1 to 2')) 'Annotations in the request are drawn and named'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"title":"Target","kind":"Bar","annotations":[{"axis":"Y","from":25,"label":"Target"}],"series":[{"name":"S","points":[{"x":0,"y":10,"label":"A"},{"x":1,"y":40,"label":"B"}]}]}'
# The value axis runs along X from 0 at 160 to 40 at 870, so 25 sits at 603.75, and the plot spans 78 to 344 down the page.
$line=([xml]$r.Content).SelectNodes('//*[local-name()="g"][@aria-label="Target: 25"]/*[local-name()="line"]')|Select-Object -Last 1
Verify ($line.x1 -eq '603.75' -and $line.x2 -eq '603.75' -and $line.y1 -eq '78' -and $line.y2 -eq '344') 'A value annotation on a horizontal bar chart stands upright at its value'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Donut","annotations":[{"axis":"Y","from":1}],"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An annotation on a chart without axes is rejected'
$branded='{"title":"Brand","kind":"Line","style":{"background":"#F6F3EE","series":["#1D4E89"],"fontFamily":"Georgia,serif"},"series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $branded
Verify ($r.Content.Contains('background:#F6F3EE') -and $r.Content.Contains("fill='#1D4E89'") -and $r.Content.Contains('font-family:Georgia,serif')) 'A style in the request brands the SVG'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","style":{"fontFamily":"Arial;background:url(x)"},"series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A style that could escape the markup is rejected'
$zoned='{"title":"Effort","kind":"Line","yZones":{"zones":[{"name":"Easy","upper":120},{"name":"Steady","upper":140},{"name":"Hard","upper":"Infinity"}]},"series":[{"name":"Heart rate","zones":{"zones":[{"name":"Easy","upper":120},{"name":"Steady","upper":140},{"name":"Hard","upper":"Infinity"}]},"points":[{"x":0,"y":110},{"x":1,"y":150},{"x":2,"y":130}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned
$strokes=@([regex]::Matches($r.Content,"fill='none' stroke='(#[0-9A-F]{6})' stroke-width='1.6' stroke-linejoin='round' stroke-linecap='round' vector-effect='non-scaling-stroke'")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($strokes.Count -ge 3 -and $r.Content.Contains('Heart rate: 1, 150, Hard')) 'A zone-coloured line posted as JSON changes stroke colour at its bounds and names each zone'
Verify ($r.Content.Contains('>Easy: up to 120<') -and $r.Content.Contains('>Steady: 120 to 140<') -and $r.Content.Contains('>Hard: above 140<')) 'Zone bands render their names and ranges'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned.Replace('"kind":"Line",','"kind":"Line","style":{"finish":"Classic"},')
$classic=@([regex]::Matches($r.Content,"fill='none' stroke='(#[0-9A-F]{6})' stroke-width='2.5' stroke-linejoin='round'/>")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($r.StatusCode -eq 200 -and $classic.Count -ge 3 -and -not $r.Content.Contains('vector-effect') -and -not $r.Content.Contains('lumen-marker') -and -not $r.Content.Contains("stroke-width='1.6'")) 'A classic finish posted as JSON draws the 2.5-pixel strokes and visible markers of 0.23.0'
$coloured='{"title":"Time in zone","kind":"Bar","yFormat":"Duration","series":[{"name":"Time","points":[{"x":0,"y":600,"label":"Easy","color":"#848484"},{"x":1,"y":1500,"label":"Steady","color":"#3F87D9"},{"x":2,"y":300,"label":"Hard","color":"#2E9B58"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $coloured
Verify ($r.Content.Contains("fill='#848484'") -and $r.Content.Contains("fill='#3F87D9'") -and $r.Content.Contains("fill='#2E9B58'") -and $r.Content.Contains('Time: Steady, 25:00')) 'Point colours posted as JSON colour their bars'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"StackedColumn","series":[{"name":"A","zones":{"zones":[{"name":"All","upper":"Infinity"}]},"points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Zones on a stacked column are rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Candlestick","series":[{"name":"P","points":[{"x":0,"open":10,"high":12,"low":9,"close":11,"color":"#123456"}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A point colour on a candlestick is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $zoned.Replace('"upper":140','"upper":100') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'A zone scale whose bounds do not rise is a bad request, not a server error'
$mixed='{"title":"Training","kind":"Line","y2Label":"Form","series":[{"name":"Fitness","projectedFrom":2,"points":[{"x":0,"y":40},{"x":1,"y":42},{"x":2,"y":45},{"x":3,"y":44}]},{"name":"Stress","kind":"Column","points":[{"x":0,"y":80},{"x":1,"y":0},{"x":2,"y":120},{"x":3,"y":60}]},{"name":"Form","kind":"Area","secondary":true,"points":[{"x":0,"y":-5},{"x":1,"y":3},{"x":2,"y":-1},{"x":3,"y":2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $mixed
$xml=[xml]$r.Content
$bars=@($xml.SelectNodes('//*[local-name()="g"][starts-with(@aria-label,"Stress: ")]/*[local-name()="rect"]'))
$strokes=@($xml.SelectNodes('//*[local-name()="path"][@fill="none"][@stroke-width="1.6"][@vector-effect="non-scaling-stroke"]'))
Verify ($r.StatusCode -eq 200 -and $bars.Count -eq 4 -and $strokes.Count -ge 2 -and $r.Content.Contains("fill-opacity='.12'")) 'A mixed chart posted as JSON renders its lines, its columns and its area'
Verify (@($strokes|Where-Object{$_.GetAttribute('stroke-dasharray') -eq '4.4 5.6'}).Count -ge 1 -and $r.Content.Contains('Fitness: 3, 44, projected') -and -not $r.Content.Contains('Fitness: 1, 42, projected')) 'A projected series posted as JSON renders a dashed stroke and names its projected marks'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $mixed.Replace('"kind":"Line",','"kind":"Line","yAxis":"Log",').Replace('{"x":1,"y":0}','{"x":1,"y":10}') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('logarithmic')) 'A column series on a log axis is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Donut","series":[{"name":"S","kind":"Line","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('own kind')) 'A series kind on a donut is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Line","series":[{"name":"S","kind":"Column","projectedFrom":1,"points":[{"x":0,"y":1},{"x":1,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('lines or areas')) 'A projection on a column series is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body '{"kind":"Line","series":[{"name":"Load","points":[{"x":0,"y":5}]},{"name":"Target","kind":"Band","points":[{"x":0,"y":5,"low":4,"high":6}]}]}'
Verify ($r.Content.StartsWith('Series,X,Y,Label,Size,Low,High') -and $r.Content.Contains('"Load",0,5,"",1,,') -and $r.Content.Contains('"Target",0,5,"",1,4,6')) 'CSV carries the edges of a band drawn on a line chart'
$market='{"title":"Market","kind":"Candlestick","xAxis":"Time","skipWeekends":true,"panes":[{"label":"Volume","weight":0.4,"yFormat":"Compact"}],"series":[{"name":"ACME","points":[{"x":1772409600000,"open":10,"high":12,"low":9,"close":11},{"x":1772496000000,"open":11,"high":13,"low":10,"close":10.4},{"x":1772582400000,"open":10.4,"high":11,"low":9.8,"close":10.9}]},{"name":"Volume","kind":"Column","pane":1,"points":[{"x":1772409600000,"y":1500000},{"x":1772496000000,"y":2100000},{"x":1772582400000,"y":1800000}]},{"name":"Average","kind":"Line","points":[{"x":1772409600000,"y":11},{"x":1772496000000,"y":10.7},{"x":1772582400000,"y":10.77}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $market -SkipHttpErrorCheck
$clips=@(([xml]$r.Content).DocumentElement.ChildNodes|Where-Object{$_.LocalName -eq 'svg'})
Verify ($r.StatusCode -eq 200 -and $clips.Count -eq 2 -and @($clips[0].SelectNodes('.//*[contains(@aria-label,": open ")]')).Count -eq 3 -and @($clips[1].SelectNodes('.//*[starts-with(@aria-label,"Volume: ")]/*[local-name()="rect"]')).Count -eq 3 -and $r.Content.Contains('Volume: 3 Mar 2026, 2.1M') -and $r.Content.Contains('Average: 4 Mar 2026, 10.77')) 'A candlestick with a volume pane posted as JSON draws its candles and average above and its volume beneath'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $market.Replace('"pane":1','"pane":2') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('Panes[k - 1]')) 'A series in a pane no ChartPane describes is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","panes":[{"label":"Below"}],"series":[{"name":"A","points":[{"x":0,"y":1}]},{"name":"B","pane":1,"points":[{"x":0,"y":2}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('continuous X axis')) 'Panes on a column chart are rejected'
$finish='{"title":"Finish","kind":"Area","style":{"gridlines":"Dotted"},"series":[{"name":"Climb","curve":"Smooth","fill":"Fade","markers":"None","points":[{"x":0,"y":10},{"x":1,"y":30},{"x":2,"y":20},{"x":3,"y":40}]},{"name":"Heart rate","kind":"Line","curve":"Smooth","strokeWidth":3,"highlightLast":true,"gradient":[{"value":10,"color":"#3F87D9"},{"value":40,"color":"#DD4B45"}],"points":[{"x":0,"y":12},{"x":1,"y":25},{"x":2,"y":38},{"x":3,"y":30}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish -SkipHttpErrorCheck
$gradients=@(([xml]$r.Content).SelectNodes('//*[local-name()="linearGradient"]'))
$paints=@([regex]::Matches($r.Content,"url\(([^)]*)\)")|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
Verify ($r.StatusCode -eq 200 -and $gradients.Count -eq 2 -and $r.Content.Contains(' C') -and $r.Content.Contains("stroke-dasharray='1 3'") -and $r.Content.Contains("r='10'")) 'A smooth, faded, gradient chart posted as JSON renders its gradients'
Verify ($paints.Count -eq 2 -and @($paints|Where-Object{-not $_.StartsWith('#')}).Count -eq 0 -and @($gradients|Where-Object{$paints -notcontains ('#'+$_.id)}).Count -eq 0 -and -not $r.Content.Contains('href') -and ([regex]::Matches($r.Content,'http://')).Count -eq 1) 'A chart with gradients stays self-contained'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish.Replace('"strokeWidth":3,','"strokeWidth":3,"zones":{"zones":[{"name":"Low","upper":20},{"name":"High","upper":"Infinity"}]},') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('one or the other')) 'A gradient together with zones is rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $finish.Replace('"strokeWidth":3','"strokeWidth":20') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('0.5 and 12')) 'A stroke width of 20 is rejected'
$r=Invoke-WebRequest "$BaseUrl/sports" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and ([regex]::Matches($r.Content,'class="lumen-chart lumen-spark"')).Count -eq 3 -and $r.Content.Contains('id="getting-faster"') -and ([regex]::Matches($r.Content,"viewBox='0 0 120 32'")).Count -eq 2 -and $r.Content.Contains("viewBox='0 0 270 54'") -and ([regex]::Matches($r.Content,"&#183; PB'")).Count -eq 10) 'The Sports & performance page prerenders its Getting faster? card: three sparklines at 120 by 32 and 270 by 54, its ten personal bests named so'
$spark='{"title":"5 km: 24:10 to 22:47 over 4 races","description":"Faster higher","kind":"Line","width":120,"height":32,"sparkline":true,"yReversed":true,"yFormat":"Duration","series":[{"name":"5 km","color":"#B7BCC4","strokeWidth":2,"points":[{"x":0,"y":1450,"label":"Race 1"},{"x":1,"y":1432,"label":"Race 2","highlight":"#E30613","valueNote":" PB"},{"x":2,"y":1445,"label":"Race 3"},{"x":3,"y":1367,"label":"Race 4","highlight":"#E30613","valueNote":" PB"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $spark -SkipHttpErrorCheck
$xml=[xml]$r.Content
Verify ($r.StatusCode -eq 200 -and @($xml.SelectNodes('//*[local-name()="text"]')).Count -eq 0 -and $xml.DocumentElement.title -eq '5 km: 24:10 to 22:47 over 4 races' -and $xml.DocumentElement.viewBox -eq '0 0 120 32' -and @($xml.SelectNodes('//*[@data-point]')).Count -eq 4 -and $r.Content.Contains("aria-label='5 km: Race 4, 22:47 PB'") -and ([regex]::Matches($r.Content,"r='5.5' fill='#E30613'")).Count -eq 2) 'A sparkline posted as JSON draws its data alone: no text, its title its name, four named marks and its two bests ringed'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $spark.Replace('"width":120,"height":32','"width":60,"height":16') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains("viewBox='0 0 60 16'")) 'A sparkline may be as small as 60 by 16'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $spark.Replace('"sparkline":true','"sparkline":false') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('320')) 'A chart that is not a sparkline is refused at 120 by 32'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $spark.Replace('"kind":"Line"','"kind":"Bubble"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('A sparkline draws a line, an area, scatter points or columns')) 'A sparkline of another kind is refused with its reason'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Area","series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2,"highlight":"#E30613"}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('A highlight rings one point of a line or scatter series')) 'A highlight on an area is refused with its reason'
$span='{"title":"Weight","kind":"Line","yMinSpan":8,"series":[{"name":"Weight","points":[{"x":0,"y":37.9},{"x":1,"y":37.8},{"x":2,"y":38.2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $span -SkipHttpErrorCheck
$ticks=@(([xml]$r.Content).SelectNodes('//*[local-name()="text"][@text-anchor="end"]')|ForEach-Object{$_.InnerText})
Verify ($r.StatusCode -eq 200 -and $ticks[0] -eq '34' -and $ticks[-1] -eq '42') 'A minimum span posted as JSON centres a narrow axis on its data, from 34 to 42'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $span.Replace('"yMinSpan":8','"yMinSpan":8,"yMin":30') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('one or the other')) 'A minimum span beside YMin is refused with its reason'
$field='{"title":"How the field finished","description":"300 finishers, median 47:12","kind":"Blocks","width":340,"height":240,"includeZero":true,"xFormat":"Duration","xTickLabels":"Bounds","yTickLabels":"Bounds","annotations":[{"axis":"X","from":2832,"label":"median","showValue":false,"inFront":true}],"series":[{"name":"Finishers","points":[{"x":2100,"xEnd":2400,"y":299},{"x":2400,"xEnd":2700,"y":1,"valueNote":" you","color":"#E30613"},{"x":2700,"xEnd":3000,"y":0}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $field -SkipHttpErrorCheck
$doc=[xml]$r.Content
$texts=@($doc.SelectNodes('//*[local-name()="text"]')|ForEach-Object{$_.InnerText})
$median=@($doc.SelectNodes('//*[local-name()="g"][@role="img"]')|ForEach-Object{$_.GetAttribute('aria-label')})
$names=@($doc.SelectNodes('//*[local-name()="g"][@data-point]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and $texts -contains 'median' -and $median -contains 'median: 47:12' -and $texts -contains '35:00' -and $texts -contains '50:00' -and $texts -contains '299' -and $names -contains 'Finishers: 40:00 to 45:00, 1 you') 'A finish-time histogram posted as JSON labels its axes at their bounds and draws its median as "median" alone, named with its time'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $field.Replace('"showValue":false','"showValue":false,"label":null').Replace('"label":"median",','') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('so it needs a Label to draw')) 'An annotation that hides its value with no label to draw is refused with its reason'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body '{"kind":"Column","xTickLabels":"Bounds","series":[{"name":"S","points":[{"x":0,"y":1}]}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains('XTickLabels chooses which labels a continuous X axis writes')) 'X tick labelling on a category chart is refused with its reason'
$long='{"title":"Field","description":"Every finisher''s time in five-minute bins from the 1st to the 99th percentile, the reader''s own in red","width":340,"height":240,"kind":"Line","series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $long -SkipHttpErrorCheck
$lines=@(([xml]$r.Content).SelectNodes('//*[local-name()="text"][@x="24"][@font-size="11"]')|Where-Object{[double]$_.GetAttribute('y') -lt 70})
Verify ($r.StatusCode -eq 200 -and $lines.Count -eq 2 -and $lines[1].GetAttribute('y') -eq '63' -and ([xml]$r.Content).DocumentElement.desc.Contains('the reader''s own in red')) 'A description too wide for a 340-pixel chart posted as JSON goes on over a second line, its whole kept in its desc'
# 0.36.0: a Y axis held symmetric about zero, values written with their sign, and a shared readout that never changes the drawing.
$form='{"title":"Form","kind":"Line","ySymmetric":10,"yFormat":"Signed","series":[{"name":"Form","valueLabels":true,"points":[{"x":0,"y":-3,"label":"Mon"},{"x":1,"y":4,"label":"Tue"},{"x":2,"y":0,"label":"Wed"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $form -SkipHttpErrorCheck
$doc=[xml]$r.Content
$ticks=@($doc.SelectNodes('//*[local-name()="text"][@text-anchor="end"][@class="lumen-muted"]')|ForEach-Object{$_.InnerText})
$names=@($doc.SelectNodes('//*[local-name()="g"][@data-point]')|ForEach-Object{$_.GetAttribute('aria-label')})
$minus=[string][char]0x2212
Verify ($r.StatusCode -eq 200 -and $ticks[0] -eq "${minus}10" -and $ticks[-1] -eq '+10' -and $ticks -contains '0' -and $names -contains "Form: Mon, ${minus}3" -and $names -contains 'Form: Tue, +4' -and $names -contains 'Form: Wed, 0') 'A symmetric, signed axis posted as JSON runs from -10 to +10 about zero, its values written with a plus or a true minus'
$paned='{"title":"Fitness and form","kind":"Line","sharedReadout":true,"panes":[{"label":"Form","ySymmetric":10,"yFormat":"Signed"}],"series":[{"name":"Fitness","points":[{"x":0,"y":50},{"x":1,"y":52}]},{"name":"Form","pane":1,"points":[{"x":0,"y":-25},{"x":1,"y":4}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $paned -SkipHttpErrorCheck
$texts=@(([xml]$r.Content).SelectNodes('//*[local-name()="text"]')|ForEach-Object{$_.InnerText})
Verify ($r.StatusCode -eq 200 -and $texts -contains "${minus}20" -and $texts -contains '+20' -and $r.Content.Contains("aria-label='Form: 0, ${minus}25'")) 'A pane''s symmetric axis posted as JSON reaches as far as its data either way, here 25'
$without=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $paned.Replace('"sharedReadout":true,','') -SkipHttpErrorCheck
Verify ($without.StatusCode -eq 200 -and $without.Content -eq $r.Content -and -not $r.Content.Contains('readout')) 'A shared readout posted as JSON draws the same SVG as a chart without it'
foreach($bad in @(@{body=$form.Replace('"ySymmetric":10','"ySymmetric":10,"yMin":-20');reason='one or the other';name='A symmetric axis beside YMin'},
  @{body=$form.Replace('"ySymmetric":10','"ySymmetric":-1');reason='positive and finite';name='A symmetric axis of -1'},
  @{body=$form.Replace('"ySymmetric":10','"ySymmetric":10,"yAxis":"Log"').Replace('"y":-3,','"y":3,').Replace('"y":0,','"y":1,');reason='logarithmic axis';name='A symmetric logarithmic axis'},
  @{body=$paned.Replace('"ySymmetric":10','"ySymmetric":10,"yMinSpan":8');reason='one or the other';name='A pane''s symmetric axis beside YMinSpan'},
  @{body='{"kind":"Donut","sharedReadout":true,"series":[{"name":"D","points":[{"x":0,"y":1}]}]}';reason='SharedReadout reads every series at one X';name='A shared readout on a donut'},
  @{body='{"kind":"Line","width":120,"height":32,"sparkline":true,"sharedReadout":true,"series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}';reason='takes no shared readout';name='A shared readout on a sparkline'},
  @{body=$form.Replace('"yFormat":"Signed"','"yFormat":"Signs"');reason='';name='An unknown value format'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.37.0: ride channels. A long line averaged into slices or thinned over the window shown, six plots named above them, and a pane's own
# tick labelling, TickLabels.None among them.
$wave=(0..999|ForEach-Object{'{"x":'+$_+',"y":'+[string]([math]::Round(100+40*[math]::Sin($_/90),1)).ToString([Globalization.CultureInfo]::InvariantCulture)+'}'}) -join ','
$averaged='{"title":"Ride","kind":"Line","sampling":"Average","maxRenderedPoints":100,"series":[{"name":"Power","points":['+$wave+']}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $averaged -SkipHttpErrorCheck
$names=@(([xml]$r.Content).SelectNodes('//*[local-name()="g"][@data-point]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and $names.Count -eq 100 -and @($names|Where-Object{$_.EndsWith(', average of 10 points')}).Count -eq 100 -and $names[0] -like 'Power: 4.5, *') 'A thousand points posted as JSON with "sampling":"Average" and a budget of 100 draw 100 averages of 10 points each'
$minmax=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $averaged.Replace('"sampling":"Average",','"sampling":"MinMax",') -SkipHttpErrorCheck
$plain=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $averaged.Replace('"sampling":"Average",','') -SkipHttpErrorCheck
Verify ($minmax.StatusCode -eq 200 -and $minmax.Content -eq $plain.Content -and -not $plain.Content.Contains('average of')) '"sampling":"MinMax" posted as JSON draws what a spec that leaves it out draws'
$zoomed=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $averaged.Replace('"maxRenderedPoints":100,','"maxRenderedPoints":100,"xMin":200,"xMax":260,') -SkipHttpErrorCheck
$points=@(([xml]$zoomed.Content).SelectNodes('//*[local-name()="g"][@data-point]')|ForEach-Object{[int]$_.GetAttribute('data-point')})
Verify ($zoomed.StatusCode -eq 200 -and $points.Count -eq 63 -and $points[0] -eq 199 -and $points[-1] -eq 261 -and -not $zoomed.Content.Contains('average of')) 'Zoomed to 200 to 260 by xMin and xMax, the long line draws every point in view and the nearest one outside each side'
$panes=(1..5|ForEach-Object{'{"label":"Channel '+$_+' \u00b7 avg 1'+$_+' \u00b7 max 2'+$_+' \u00b7 min 0","weight":1'+$(if($_ -eq 2){',"yTickLabels":"All"'}else{''})+'}'}) -join ','
$series=(0..5|ForEach-Object{'{"name":"C'+$_+'","pane":'+$_+',"markers":"None","strokeWidth":1.5,"points":[{"x":0,"y":'+$_+'},{"x":600,"y":'+(10+$_)+'},{"x":1200,"y":'+(5+$_)+'}]}'}) -join ','
$channels='{"title":"Channels","kind":"Line","height":640,"xFormat":"Duration","yTickLabels":"None","paneTitles":"Above","yLabel":"Heart rate \u00b7 avg 148 \u00b7 max 182 \u00b7 min 96 bpm","panes":['+$panes+'],"series":['+$series+']}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $channels -SkipHttpErrorCheck
$doc=[xml]$r.Content
$headers=@($doc.SelectNodes('//*[local-name()="text"][@class="lumen-pane-title"]')|ForEach-Object{$_.InnerText})
$ticks=@($doc.SelectNodes('//*[local-name()="text"][@text-anchor="end"][@class="lumen-muted"]'))
$rotated=@($doc.SelectNodes('//*[local-name()="text"][starts-with(@transform,"rotate(-90")]'))
Verify ($r.StatusCode -eq 200 -and $headers.Count -eq 6 -and $headers[0] -eq ('Heart rate {0} avg 148 {0} max 182 {0} min 96 bpm' -f [char]0x00B7) -and $headers[2] -like 'Channel 2 *' -and $ticks.Count -ge 1 -and $rotated.Count -eq 0) 'Six plots posted as JSON with "paneTitles":"Above" are each named above their plot, and only the pane set to "yTickLabels":"All" labels its ticks'
$bare=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $channels.Replace(',"yTickLabels":"All"','') -SkipHttpErrorCheck
$clips=@(([xml]$bare.Content).DocumentElement.ChildNodes|Where-Object{$_.LocalName -eq 'svg'})
Verify ($bare.StatusCode -eq 200 -and @(([xml]$bare.Content).SelectNodes('//*[local-name()="text"][@text-anchor="end"][@class="lumen-muted"]')).Count -eq 0 -and $clips.Count -eq 6 -and $clips[0].GetAttribute('x') -eq '24') 'With no tick label written up the left, the six plots posted as JSON stand 30 pixels from the left edge'
$axis=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $channels.Replace('"paneTitles":"Above",','"paneTitles":"Axis",') -SkipHttpErrorCheck
Verify ($axis.StatusCode -eq 200 -and -not $axis.Content.Contains('lumen-pane-title') -and @(([xml]$axis.Content).SelectNodes('//*[local-name()="text"][starts-with(@transform,"rotate(-90")]')).Count -eq 6) '"paneTitles":"Axis" posted as JSON writes each plot''s name up the side, as before'
$seventh=$channels.Replace('"panes":[','"panes":[{"label":"Extra"},').Replace('"series":[','"series":[{"name":"C6","pane":6,"points":[{"x":0,"y":1}]},')
foreach($bad in @(@{body=$seventh;reason='at most six plots';name='A seventh plot'},
  @{body=$channels.Replace('"yTickLabels":"None"','"yTickLabels":"None","xTickLabels":"None"');reason='TickLabels.None leaves a Y axis unlabelled';name='X tick labels set to None'},
  @{body='{"kind":"Donut","paneTitles":"Above","series":[{"name":"D","points":[{"x":0,"y":1}]}]}';reason='PaneTitles names each plot above it';name='Pane titles above a donut'},
  @{body='{"kind":"Line","width":120,"height":32,"sparkline":true,"paneTitles":"Above","series":[{"name":"S","points":[{"x":0,"y":1},{"x":1,"y":2}]}]}';reason='names no plot above it';name='Pane titles above a sparkline'},
  @{body=$averaged.Replace('"sampling":"Average"','"sampling":"Median"');reason='';name='An unknown sampling method'},
  @{body=$channels.Replace('"paneTitles":"Above"','"paneTitles":"Below"');reason='';name='An unknown pane title placement'},
  @{body=$channels.Replace(',"yTickLabels":"All"',',"yTickLabels":"Some"');reason='';name='An unknown pane tick labelling'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.38.0: ticks set by hand, a unit after every value, end labels, and their refusals.
$gap='{"title":"Gap","kind":"Line","width":340,"height":320,"yReversed":true,"yMin":0,"yFormat":"Signed","yUnit":"s","yTickValues":[{"value":0,"label":"Leader"},{"value":10},{"value":99,"label":"Outside"}],"series":[{"name":"Rider A","endLabel":"A","endNote":"leader","points":[{"x":0,"y":0},{"x":1,"y":0}]},{"name":"You","endLabel":"You","endNote":"+12s","points":[{"x":0,"y":0},{"x":1,"y":12}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $gap -SkipHttpErrorCheck
$doc=[xml]$r.Content
$ticks=@($doc.SelectNodes('//*[local-name()="text"][@text-anchor="end"][@class="lumen-muted"]')|ForEach-Object{$_.InnerText})
Verify ($r.StatusCode -eq 200 -and ($ticks -join ',') -eq 'Leader,+10s') 'Ticks posted as "yTickValues" stand where they are set, a label written as given, an unlabelled one in the signed format with its "yUnit", and one outside the axis left out'
$ends=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-end"]'))
$named=@($doc.SelectNodes('//*[local-name()="g"][@data-series="1"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($ends.Count -eq 2 -and $ends[1].InnerText.Contains('You +12s') -and $named[-1] -eq ('You: 1, +12s, labelled You {0} +12s' -f [char]0x00B7)) '"endLabel" and "endNote" posted as JSON are written at the end of each line and said in its last point''s name, its value with its unit'
$pane='{"title":"Paned","kind":"Line","panes":[{"label":"Power","yUnit":" W","yTickValues":[{"value":250,"label":"FTP"}]}],"series":[{"name":"HR","points":[{"x":0,"y":140},{"x":1,"y":150}]},{"name":"Power","pane":1,"points":[{"x":0,"y":200},{"x":1,"y":300}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $pane -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('>FTP</text>') -and $r.Content.Contains("aria-label='Power: 1, 300 W'") -and $r.Content.Contains("aria-label='HR: 1, 150'")) 'A pane posted with its own "yUnit" and "yTickValues" writes them on its own axis alone'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $gap -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('"You",1,12') -and -not $r.Content.Contains('12s')) 'CSV keeps raw numbers whatever the "yUnit"'
foreach($bad in @(@{body=$gap.Replace('{"value":10}','{"value":0}');reason='Each value in YTickValues stands once';name='A tick value posted twice'},
  @{body=$gap.Replace('"yUnit":"s"','"yUnit":"seconds!!"');reason='YUnit is written after every value';name='A unit past 8 characters'},
  @{body=$gap.Replace('"endLabel":"A"','"endLabel":"A rider name far too long"');reason='at most 24 characters';name='An end label past 24 characters'},
  @{body=$gap.Replace('"endLabel":"A",','');reason='EndNote is written after';name='An end note without its label'},
  @{body='{"kind":"Column","series":[{"name":"C","endLabel":"C","points":[{"x":0,"y":1}]}]}';reason='applies to series drawn as lines, areas or scatter points';name='An end label on columns'},
  @{body='{"kind":"Donut","yUnit":"%","series":[{"name":"D","points":[{"x":0,"y":1}]}]}';reason='YUnit follows the values a Y axis measures';name='A unit on a donut'},
  @{body='{"kind":"Histogram","yTickValues":[{"value":1}],"series":[{"name":"H","points":[{"x":0,"y":1}]}]}';reason='YTickValues sets the ticks';name='Ticks set by hand on a histogram'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.39.0: a strip of parts, bars on tracks, titles kept but not drawn, and their refusals.
$strip='{"title":"Effort zones","description":"Time in each zone","kind":"Strip","width":340,"height":16,"yFormat":"Duration","series":[{"name":"Zones","points":[{"x":0,"y":740,"label":"Easy","color":"#3FD17A"},{"x":1,"y":1290,"label":"Moderate","color":"#D7DDE5"},{"x":2,"y":0,"label":"Hard"},{"x":3,"y":250,"label":"Very hard","color":"#E30613"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $strip -SkipHttpErrorCheck
$doc=[xml]$r.Content
$keys=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-strip-key"]/*[local-name()="text"]')|ForEach-Object{$_.InnerText})
$parts=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and ($keys -join '|') -eq 'Easy 32%|Moderate 57%|Hard 0%|Very hard 11%' -and ($parts -join '|') -eq 'Easy: 32%, 12:20|Moderate: 57%, 21:30|Very hard: 11%, 4:10') 'A strip posted as "kind":"Strip" draws a part for each amount above zero, named with its share and its time, and keys every part with whole percentages adding up to 100, a zone of none included'
Verify ($doc.DocumentElement.GetAttribute('viewBox') -eq '0 0 340 142' -and -not $r.Content.Contains('font-size=''11''>Zones</text>')) 'A strip is drawn as tall as its title, bar and key, whatever "height" says, with no series legend under its own key'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $strip -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('"Zones",1,1290,"Moderate"')) 'A strip exports its parts as CSV rows'
$scores='{"title":"Race scores","description":"Each out of 100","kind":"Bar","width":340,"height":240,"yMin":0,"yMax":100,"barTrack":true,"yTickLabels":"None","drawTitles":false,"series":[{"name":"Score","valueLabels":true,"points":[{"x":0,"y":82,"label":"Execution"},{"x":1,"y":64,"label":"Improvement"},{"x":2,"y":108,"label":"Effort"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $scores -SkipHttpErrorCheck
$doc=[xml]$r.Content
$tracks=@($doc.SelectNodes('//*[@class="lumen-bar-track"]'))
$named=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and $tracks.Count -eq 3 -and $named[2] -eq 'Score: Effort, 108, above the scale, drawn at 100' -and $r.Content.Contains('>108</text>')) '"barTrack" posted as JSON draws a track behind each bar to "yMax", a score past it drawn at its end and named so, its value label kept'
Verify (-not $r.Content.Contains('>Race scores</text>') -and -not $r.Content.Contains('>Each out of 100</text>') -and $r.Content.Contains('<title>Race scores</title><desc>Each out of 100</desc>') -and $r.Content.Contains("aria-label='Race scores. Each out of 100'")) '"drawTitles":false keeps the title and description as the drawing''s title, desc and name without drawing them'
foreach($bad in @(@{body=$strip.Replace('"series":[{','"series":[{"name":"Other","points":[{"x":0,"y":1,"label":"A"}]},{');reason='A strip draws the parts of one whole';name='A strip of two series'},
  @{body=$strip.Replace('"y":740','"y":-740');reason='A part of a strip is an amount';name='A negative part'},
  @{body=$strip.Replace('"y":740','"y":0').Replace('"y":1290','"y":0').Replace('"y":250','"y":0');reason='parts that are all zero have no whole to share';name='A strip of zeros'},
  @{body=$strip.Replace('"yFormat":"Duration"','"yFormat":"Duration","annotations":[{"axis":"X","from":1}]');reason='so it takes no annotations';name='An annotation on a strip'},
  @{body=$strip.Replace('"name":"Zones",','"name":"Zones","valueLabels":true,');reason='so it takes no value labels';name='Value labels on a strip'},
  @{body=$strip.Replace('"width":340','"width":300');reason='must be 320';name='A strip narrower than 320'},
  @{body=$scores.Replace('"yMax":100,','');reason='so BarTrack needs YMax';name='A track without a maximum'},
  @{body=$scores.Replace('"kind":"Bar"','"kind":"StackedColumn"');reason='BarTrack draws a track behind each bar';name='A track on stacked columns'},
  @{body=$scores.Replace('"yMin":0','"yMin":-10');reason='so BarTrack needs an axis that starts at zero';name='A track below zero'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.40.0: columns and bars filled by value, a second line under each category's name, a strip part said once, and their refusals.
$laps='{"title":"Heart rate per lap","description":"Average heart rate in each lap","kind":"Column","width":340,"height":260,"xLabel":"Lap","series":[{"name":"Heart rate","gradient":[{"value":152,"color":"#A88200"},{"value":174,"color":"#DD4B45"}],"points":[{"x":0,"y":152,"label":"L1","subLabel":"152 bpm"},{"x":1,"y":161,"label":"L2","subLabel":"161 bpm"},{"x":2,"y":168,"label":"L3","subLabel":"168 bpm"},{"x":3,"y":174,"label":"L4","subLabel":"174 bpm"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $laps -SkipHttpErrorCheck
$doc=[xml]$r.Content
$gradients=@($doc.SelectNodes('//*[local-name()="linearGradient"]'))
$columns=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-datum"]/*[local-name()="rect"]'))
$named=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and $gradients.Count -eq 1 -and $gradients[0].GetAttribute('gradientUnits') -eq 'userSpaceOnUse' -and $gradients[0].GetAttribute('x1') -eq '0' -and @($columns|Where-Object{$_.GetAttribute('fill') -eq "url(#$($gradients[0].GetAttribute('id')))"}).Count -eq 4) 'A column series posted with "gradient" fills every column with one gradient laid up the value axis'
Verify ([double]$gradients[0].GetAttribute('y1') -eq [double]$columns[0].GetAttribute('y') -and [double]$gradients[0].GetAttribute('y2') -eq [double]$columns[3].GetAttribute('y')) 'The gradient''s first stop stands at the gentlest lap''s top and its last at the hardest''s, so a taller column reaches further along it'
$subs=@($doc.SelectNodes('//*[local-name()="text"][@font-size="11"][@class="lumen-muted"][@text-anchor="middle"]')|ForEach-Object{$_.InnerText})
Verify (($subs -join '|') -eq '152 bpm|161 bpm|168 bpm|174 bpm' -and $named[2] -eq "Heart rate: L3 $([char]0xB7) 168 bpm, 168") '"subLabel" posted as JSON writes a second line under each lap''s name, said after the name in its mark''s name'
$bars='{"title":"Best efforts","kind":"Bar","series":[{"name":"Power","gradient":[{"value":240,"color":"#3F87D9"},{"value":780,"color":"#DD4B45"}],"points":[{"x":0,"y":780,"label":"5s","subLabel":"15.0 W/kg"},{"x":1,"y":420,"label":"1m"},{"x":2,"y":240,"label":"20m","subLabel":"4.6 W/kg"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bars -SkipHttpErrorCheck
$doc=[xml]$r.Content
$gradient=$doc.SelectSingleNode('//*[local-name()="linearGradient"]')
Verify ($r.StatusCode -eq 200 -and $gradient.GetAttribute('y1') -eq '0' -and $gradient.GetAttribute('y2') -eq '0' -and [double]$gradient.GetAttribute('x2') -gt [double]$gradient.GetAttribute('x1') -and $r.Content.Contains('>15.0 W/kg</text>')) 'A bar chart''s gradient runs across the plot along X, and its sub-labels stand under the names beside the bars'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $laps -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.StartsWith('Series,X,Y,Label,Size') -and $r.Content.Contains('"Heart rate",2,168,"L3"')) 'A chart with sub-labels exports its observations as CSV, as before'
$shares='{"title":"Zones","kind":"Strip","width":340,"yUnit":"%","series":[{"name":"Zones","points":[{"x":0,"y":30,"label":"Easy"},{"x":1,"y":40,"label":"Moderate"},{"x":2,"y":30,"label":"Hard"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $shares -SkipHttpErrorCheck
$parts=@(([xml]$r.Content).SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify ($r.StatusCode -eq 200 -and ($parts -join '|') -eq 'Easy: 30%|Moderate: 40%|Hard: 30%') 'A strip part whose amount writes as its share is named once'
foreach($bad in @(@{body=$laps.Replace('"kind":"Column"','"kind":"StackedColumn"');reason='A stacked column''s colours tell its stacked series apart';name='A gradient on stacked columns'},
  @{body=$laps.Replace('"name":"Heart rate",','"name":"Heart rate","fill":"Fade",');reason='A faded column fades its own colour';name='A gradient beside a faded fill'},
  @{body=$laps.Replace('"kind":"Column"','"kind":"Line"');reason='A sub-label is a second line under a category''s name';name='A sub-label on a line chart'},
  @{body=$laps.Replace('"subLabel":"152 bpm"','"subLabel":"152 beats a minute"');reason='at most 16 characters';name='A sub-label past 16 characters'},
  @{body=$laps.Replace('"series":[{','"series":[{"name":"Other","points":[{"x":0,"y":150,"label":"L1","subLabel":"150 bpm"}]},{');reason='may repeat it or leave it null, but not give different ones';name='Two sub-labels for one category'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.41.0: a background left unpainted, points the app averaged, bars drawn as tall as their rows, and their refusals.
$r=Invoke-WebRequest "$BaseUrl/" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains("viewBox='0 0 340 210'") -and $r.Content.Contains('--lumen-ground:#F3F6FB') -and $r.Content.Contains('style="background:#F3F6FB"') -and $r.Content.Contains(', average of 12 s')) 'The home page prerenders a fitted meter 210 units tall, a chart left unpainted on a card of its ground''s colour, and series the app averaged'

$meters='{"title":"Scores","kind":"Bar","width":340,"height":240,"yMin":0,"yMax":100,"barTrack":true,"fitHeight":true,"yTickLabels":"None","drawTitles":false,"paintBackground":false,"style":{"background":"#F3F6FB"},"series":[{"name":"Score","valueLabels":true,"averageOf":"3 races","points":[{"x":0,"y":82,"label":"Pacing"},{"x":1,"y":64,"label":"Recovery"},{"x":2,"y":91,"label":"Technique"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $meters
$root=([xml]$r.Content).DocumentElement
Verify ($r.StatusCode -eq 200 -and $root.GetAttribute('viewBox') -eq '0 0 340 182') '"fitHeight" posted as JSON draws three meters 160 units tall and its legend row 22 under them, not the 240 asked for'
Verify (-not $root.GetAttribute('style').Contains('background:') -and $root.GetAttribute('style').Contains('--lumen-ground:#F3F6FB')) '"paintBackground":false writes no background on the root, only the colour it stands on'
$named=@(([xml]$r.Content).SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
Verify (($named -join '|') -eq 'Score: Pacing, 82, average of 3 races|Score: Recovery, 64, average of 3 races|Score: Technique, 91, average of 3 races') '"averageOf" posted as JSON ends each mark''s name with what its points average'
$plain=$meters.Replace('"fitHeight":true,','').Replace('"paintBackground":false,','').Replace('"averageOf":"3 races",','')
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $plain
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.GetAttribute('viewBox') -eq '0 0 340 262' -and $r.Content.Contains('background:#F3F6FB') -and -not $r.Content.Contains('average of')) 'Without them the chart keeps its height, its background and its plain names'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $meters
$p=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $plain
Verify ($r.StatusCode -eq 200 -and $r.Content -eq $p.Content -and -not $r.Content.Contains('average')) 'CSV keeps the values a series says are averages'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body '{"title":"Flow","paintBackground":false,"nodes":[{"id":"a","label":"Start"},{"id":"b","label":"End"}],"edges":[{"source":"a","target":"b"}]}'
Verify ($r.StatusCode -eq 200 -and -not ([xml]$r.Content).DocumentElement.GetAttribute('style').Contains('background:')) 'A graph posted with "paintBackground":false writes no background'
foreach($bad in @(@{body=$meters.Replace('"kind":"Bar"','"kind":"Column"');reason='FitHeight works out a horizontal bar chart''s height';name='FitHeight on a column chart'},
  @{body='{"title":"Line","fitHeight":true,"series":[{"name":"S","points":[{"x":0,"y":1}]}]}';reason='FitHeight works out a horizontal bar chart''s height';name='FitHeight on a line chart'},
  @{body=$meters.Replace('"averageOf":"3 races"','"averageOf":" "');reason='so it needs words';name='A blank AverageOf'},
  @{body=$meters.Replace('"averageOf":"3 races"','"averageOf":"seventeen letters"');reason='at most 16 characters on one line';name='An AverageOf past 16 characters'},
  @{body=$meters.Replace('"averageOf":"3 races"','"averageOf":"12\ns"');reason='at most 16 characters on one line';name='An AverageOf on two lines'},
  @{body='{"title":"Recovery","kind":"Gauge","series":[{"name":"Recovery","averageOf":"a week","points":[{"x":0,"y":72}]}]}';reason='AverageOf is said after a mark''s one value';name='An AverageOf on a gauge'},
  @{body='{"title":"Zones","kind":"Strip","width":340,"series":[{"name":"Zones","averageOf":"a week","points":[{"x":0,"y":30,"label":"Easy"},{"x":1,"y":70,"label":"Hard"}]}]}';reason='AverageOf is said after a mark''s one value';name='An AverageOf on a strip'},
  @{body=$meters.Replace('"height":240','"height":100');reason='240';name='A fitted chart''s height outside 240-2160'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
# 0.42.0: a missing value written as a word and named as a mark, and its refusals.
$r=Invoke-WebRequest "$BaseUrl/" -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('>absent</text>') -and $r.Content.Contains("aria-label='Share: Round 6, absent'")) 'The home page prerenders the team rider''s missed round, written absent and named as a mark'
$rider='{"title":"Team rider","kind":"Line","width":340,"height":260,"xMin":-0.5,"xMax":4.5,"yMin":0,"yMax":100,"yUnit":"%","series":[{"name":"Share","points":[{"x":0,"y":78,"label":"Round 1","valueNote":"/38"},{"x":1,"y":68,"label":"Round 2"},{"x":2,"y":null,"label":"Round 3","gapLabel":"absent","color":"#8A6500"},{"x":3,"y":85,"label":"Round 5"},{"x":4,"y":94,"label":"Round 6"}]}]}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $rider -SkipHttpErrorCheck
$doc=[xml]$r.Content
$named=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')|ForEach-Object{$_.GetAttribute('aria-label')})
$word=@($doc.SelectNodes('//*[local-name()="g"][@class="lumen-gap"]/*[local-name()="text"]'))
Verify ($r.StatusCode -eq 200 -and $named.Count -eq 5 -and $named -contains 'Share: Round 3, absent' -and $named -contains 'Share: Round 1, 78%/38' -and $word.Count -eq 2 -and $word[1].InnerText -eq 'absent' -and $word[1].GetAttribute('fill') -eq '#8A6500') '"gapLabel" posted as JSON writes the word in the point''s colour and names the missing round as a mark'
Verify (@($doc.SelectNodes('//*[local-name()="path"][@fill="none"]')).Count -eq 2) 'The line still breaks at the missing round'
$silent=$rider.Replace(',"gapLabel":"absent"','')
$p=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $silent -SkipHttpErrorCheck
Verify ($p.StatusCode -eq 200 -and -not $p.Content.Contains('absent') -and @(([xml]$p.Content).SelectNodes('//*[local-name()="g"][@class="lumen-datum"]')).Count -eq 4) 'Without "gapLabel" the missing round is silent and has no mark, as before'
$r=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $rider
$p=Invoke-WebRequest "$BaseUrl/api/charts/csv" -Method Post -ContentType application/json -Body $silent
Verify ($r.StatusCode -eq 200 -and $r.Content -eq $p.Content) 'CSV keeps a missing round''s empty value'
foreach($bad in @(@{body=$rider.Replace('"y":null,"label":"Round 3"','"y":50,"label":"Round 3"');reason='so it applies to a point whose Y is null';name='A gap label on a point with a value'},
  @{body=$rider.Replace('"kind":"Line","width":340,"height":260,"xMin":-0.5,"xMax":4.5','"kind":"Column","width":340,"height":260');reason='so it applies to series drawn as lines, areas or scatter points';name='A gap label on a column chart'},
  @{body=$rider.Replace('"width":340,"height":260','"width":120,"height":32,"sparkline":true');reason='so it writes no gap labels';name='A gap label on a sparkline'},
  @{body=$rider.Replace('"gapLabel":"absent"','"gapLabel":"did not finish"');reason='at most 12 characters and no line breaks';name='A gap label past 12 characters'},
  @{body=$rider.Replace('"gapLabel":"absent"','"gapLabel":"no\nride"');reason='at most 12 characters and no line breaks';name='A gap label on two lines'},
  @{body=$rider.Replace('"gapLabel":"absent"','"gapLabel":" "');reason='so it needs words';name='A blank gap label'},
  @{body=$rider.Replace('"kind":"Line"','"kind":"Scatter","densityCells":20').Replace(',"color":"#8A6500"','');reason='so it writes no gap label';name='A gap label on a density scatter'})){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad.body -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400 -and $r.RawContent.Contains($bad.reason)) "$($bad.name) is rejected"
}
$graph='{"nodes":[{"id":"a","label":"Start"},{"id":"b","label":"End"}],"edges":[{"source":"a","target":"b"}],"layout":"Layered"}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body $graph
Verify ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'svg') 'Graph SVG'
$positions=Invoke-RestMethod "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body $graph
Verify ($positions.Count -eq 2 -and $positions[0].x -lt $positions[1].x) 'Layered coordinates'
$routes=Invoke-RestMethod "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body $graph
Verify ($routes.Count -eq 1 -and $routes[0].points.Count -eq 2) 'Graph routes for an adjacent edge'
$spanning='{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"},{"id":"c","label":"C"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"c"},{"source":"a","target":"c"}],"layout":"Layered"}'
$routes=Invoke-RestMethod "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body $spanning
Verify ($routes[2].points.Count -eq 3) 'Long graph edges bend through each level'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body $spanning
Verify ($r.Content.Contains('data-position=') -and $r.Content.Contains('edge crossings')) 'Graph SVG exposes node handles and its crossing count'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/routes" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Cyclic route request rejected'
$down=$spanning.Replace('"layout":"Layered"','"layout":"Layered","direction":"TopToBottom"')
$positions=Invoke-RestMethod "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body $down
Verify ($positions.Count -eq 3 -and $positions[0].y -lt $positions[1].y -and $positions[1].y -lt $positions[2].y -and $positions[0].x -eq $positions[2].x) 'A layered graph set top to bottom lays its levels out in rows down the drawing'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/svg" -Method Post -ContentType application/json -Body $down
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains('Layered layout top to bottom')) 'A graph set top to bottom says so in its description'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body $spanning.Replace('"layout":"Layered"','"layout":"Layered","direction":"Sideways"') -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'An unknown graph direction is rejected'
# 0.43.0: the season planner.
$planner='{"spec":{"title":"Season planner","from":"2027-01-01","to":"2027-12-31","regions":[{"code":"ZA","name":"South Africa"},{"code":"ZA-GP","name":"Gauteng","parent":"ZA"}],"periods":[{"from":"2027-04-27","name":"Freedom Day","kind":"PublicHoliday","region":"ZA"}],"events":[{"id":"e1","name":"Hilltop XCO","start":"2027-03-13","region":"ZA-GP","relevance":"Clash"}]},"view":{"zoom":"Month","date":"2027-03-01"}}'
$r=Invoke-WebRequest "$BaseUrl/api/charts/planner/svg" -Method Post -ContentType application/json -Body $planner -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Headers['Content-Type'] -like 'image/svg+xml*' -and $r.Content.Contains("aria-label='Hilltop XCO, Saturday 13 March 2027, Gauteng, clash'")) 'Planner month draws the event named in words'
$r=Invoke-WebRequest "$BaseUrl/api/charts/planner/svg" -Method Post -ContentType application/json -Body '{"spec":{"title":"Bad","from":"2027-01-02","to":"2027-01-01"}}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Planner refuses a period that ends before it starts'
$r=Invoke-WebRequest "$BaseUrl/api/charts/planner/svg" -Method Post -ContentType application/json -Body ($planner.Replace(',"view":{"zoom":"Month","date":"2027-03-01"}',',"layout":"Narrow"')) -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200 -and $r.Content.Contains("class='lumen-month'")) 'Planner draws the narrow year'
# A missing list, a missing event or an undefined status is the caller's mistake: 400 with the rule, never a 500.
foreach($bad in @($planner.Replace('"events":[{"id"','"events":null,"x":[{"id"'),$planner.Replace('"regions":[{"code"','"regions":null,"x":[{"code"'),$planner.Replace('"events":[','"events":[null,'),$planner.Replace('"relevance":"Clash"','"relevance":9'),$planner.Replace('"title":"Season planner",','"title":"Season planner","filter":{"regions":null},'))){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/planner/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) "Planner refuses a missing list, element or undefined value with 400 ($($r.StatusCode))"
}
foreach($bad in @('{"kind":"Donut","series":[{"name":"Bad","points":[{"x":0,"y":-1}]}]}','{"width":99999}','{"series":null}','{"kind":"Bogus"}','{not json')){
 $r=Invoke-WebRequest "$BaseUrl/api/charts/svg" -Method Post -ContentType application/json -Body $bad -SkipHttpErrorCheck
 Verify ($r.StatusCode -eq 400) 'Invalid request rejected'
}
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"},{"id":"b","label":"B"}],"edges":[{"source":"a","target":"b"},{"source":"b","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 400) 'Cyclic layered graph rejected'
$r=Invoke-WebRequest "$BaseUrl/api/charts/graph/layout" -Method Post -ContentType application/json -Body '{"nodes":[{"id":"a","label":"A"}],"edges":[{"source":"a","target":"a"}]}' -SkipHttpErrorCheck
Verify ($r.StatusCode -eq 200) 'Layered self-loop accepted'
Write-Output "$checks HTTP checks passed."
