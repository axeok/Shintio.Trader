<template>
	<div class="pa-4">
		Show {{ count }} of
		<span class="text-success">{{ profitable }}</span> wins.
		Hide <span class="text-error">{{ Object.keys(data.Values).length - profitable }}</span> loses of total
		{{ Object.keys(data.Values).length }}.
		<v-slider
			v-model="count"
			:min="1"
			:max="200"
			:step="1"
		/>
		<apexchart
			width="100%"
			height="800"
			:options="options"
			:series="series"
		/>
	</div>
</template>

<script setup>
	import data from "../../../Shintio.Trader/bin/Debug/net9.0/benchmark.json";

	const count = $ref(20);

	const dates = $computed(() => {
		const startDate = new Date(data.StartTime);
		const endDate = new Date(data.EndTime);
		const stepSeconds = data.SaveStepSeconds;

		const stepMilliseconds = stepSeconds * 1000;
		const result = [];

		for (let time = startDate.getTime(); time <= endDate.getTime(); time += stepMilliseconds) {
			result.push(new Date(time));
		}

		return result;
	});

	const balances = $computed(() => {
		const result = (Object.entries(data.Values)
			.sort((a, b) => {
				const aLastValue = a[1][a[1].length - 1] ?? 0;
				const bLastValue = b[1][b[1].length - 1] ?? 0;
				return bLastValue - aLastValue;
			})
			.map((entry, index) => ({
				name: `#${index + 1} ${entry[0]}`,
				type: "line",
				data: entry[1].map((value, i) => ({
					x: dates[i],
					y: value,
				})),
			})))
			.filter(s => s.data[0].y < s.data[s.data.length - 1].y);

		return result;
	});

	const profitable = $computed(() => {
		let result = 0;

		for (const values of Object.values(data.Values)) {
			if (values[0] < values[values.length - 1]) {
				result++;
			}
		}

		return result;
	});

	const total = $computed(() => balances.length);

	const options = $ref({
		chart: {
			id: "vuechart-example",
		},
		legend: {
			position: "top",
			horizontalAlign: "left",
		},
		tooltip: {
			enabled: true,
			shared: true,
			intersect: false,
			onDatasetHover: {
				highlightDataSeries: true,
			},
			x: {
				show: true,
			},
		},
		xaxis: {
			type: "datetime",
		},
		yaxis: [
			{
				decimalsInFloat: 0,
				title: {
					text: "Balance",
				},
				opposite: true,
			},
		],
	});

	const series = $computed(() => {
		return [
			// prices,
			...evenlySpacedElements(balances, count),
			// starts,
			// ends,
			// deltaBalances,
			// winratesCount,
			// winratesSum,
		];
	});

	function evenlySpacedElements(arr, count) {
		if (count <= 1) return [arr[0]];
		const step = (arr.length - 1) / (count - 1);

		return Array.from({length: count}, (_, i) => {
			const index = Math.round(i * step);
			return arr[index];
		});
	}
</script>
