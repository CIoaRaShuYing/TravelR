<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Refresh, View } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { api, type MyPayroll } from '../api'

const loading = ref(false)
const rows = ref<MyPayroll[]>([])
const month = ref('')
const detail = reactive({ visible: false, row: null as MyPayroll | null })
const months = computed(() => [...new Set(rows.value.map(row => row.payrollMonth.slice(0, 7)))])
const filteredRows = computed(() => month.value ? rows.value.filter(row => row.payrollMonth.startsWith(month.value)) : rows.value)

function money(value: number) { return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) }
function monthLabel(value: string) { return `${value.slice(0, 4)} 年 ${value.slice(5, 7)} 月` }
function dateTime(value: string) { return new Date(value).toLocaleString('zh-CN', { hour12: false }) }
function open(row: MyPayroll) { detail.row = row; detail.visible = true }
async function load() {
  loading.value = true
  try { rows.value = await api.listMyPayroll() }
  catch (error) { ElMessage.error(api.message(error, '加载工资记录失败。')) }
  finally { loading.value = false }
}
onMounted(load)
</script>

<template>
  <section>
    <header class="page-header"><div><p class="eyebrow">MY PAYROLL</p><h1>我的工资</h1><p>仅展示已经确认发放的本人工资记录。</p></div><el-button circle :icon="Refresh" :loading="loading" @click="load" /></header>
    <div class="filter-bar"><el-select v-model="month" clearable placeholder="全部月份"><el-option v-for="value in months" :key="value" :label="monthLabel(value)" :value="value" /></el-select><span class="result-count">共 {{ filteredRows.length }} 条已发工资</span></div>
    <div class="table-shell desktop-table" v-loading="loading"><el-table :data="filteredRows" empty-text="当前没有已发工资。"><el-table-column label="工资月份" width="145"><template #default="scope"><strong>{{ monthLabel(scope.row.payrollMonth) }}</strong></template></el-table-column><el-table-column label="应发工资" min-width="135"><template #default="scope">{{ money(scope.row.grossPay) }}</template></el-table-column><el-table-column label="扣款合计" min-width="135"><template #default="scope">{{ money(scope.row.totalDeductions) }}</template></el-table-column><el-table-column label="实发工资" min-width="145"><template #default="scope"><strong>{{ money(scope.row.netPay) }}</strong></template></el-table-column><el-table-column label="发放时间" min-width="175"><template #default="scope">{{ dateTime(scope.row.paidAt) }}</template></el-table-column><el-table-column label="操作" width="70"><template #default="scope"><el-button text circle :icon="View" aria-label="查看工资详情" @click="open(scope.row)" /></template></el-table-column></el-table></div>
    <div class="mobile-list" v-loading="loading"><article v-for="row in filteredRows" :key="row.id" class="mobile-record payroll-slip-card"><div class="mobile-record__head"><div><strong>{{ monthLabel(row.payrollMonth) }}</strong><span>{{ dateTime(row.paidAt) }}</span></div><el-button text circle :icon="View" @click="open(row)" /></div><p>实发工资</p><strong class="payroll-slip-amount">{{ money(row.netPay) }}</strong><dl><div><dt>应发</dt><dd>{{ money(row.grossPay) }}</dd></div><div><dt>扣款</dt><dd>{{ money(row.totalDeductions) }}</dd></div></dl></article><el-empty v-if="!loading && filteredRows.length === 0" description="当前没有已发工资" /></div>

    <el-drawer v-model="detail.visible" title="工资明细" size="min(520px, 100vw)" class="payroll-detail-drawer"><template v-if="detail.row"><div class="payroll-slip-heading"><span>{{ monthLabel(detail.row.payrollMonth) }}</span><strong>{{ money(detail.row.netPay) }}</strong><small>发放至 •••• {{ detail.row.bankCardLastFour }} · {{ dateTime(detail.row.paidAt) }}</small></div><section class="payroll-slip-section"><h3>收入项</h3><dl><div><dt>基本工资</dt><dd>{{ money(detail.row.baseSalary) }}</dd></div><div><dt>绩效工资</dt><dd>{{ money(detail.row.performanceSalary) }}</dd></div><div><dt>奖金</dt><dd>{{ money(detail.row.bonus) }}</dd></div><div><dt>津贴</dt><dd>{{ money(detail.row.allowance) }}</dd></div><div><dt>其他增加</dt><dd>{{ money(detail.row.otherIncrease) }}</dd></div><div class="total"><dt>应发工资</dt><dd>{{ money(detail.row.grossPay) }}</dd></div></dl></section><section class="payroll-slip-section"><h3>扣款项</h3><dl><div><dt>社保个人扣款</dt><dd>{{ money(detail.row.socialSecurityDeduction) }}</dd></div><div><dt>公积金个人扣款</dt><dd>{{ money(detail.row.housingFundDeduction) }}</dd></div><div><dt>个人所得税</dt><dd>{{ money(detail.row.individualIncomeTax) }}</dd></div><div><dt>其他扣款</dt><dd>{{ money(detail.row.otherDeduction) }}</dd></div><div class="total"><dt>扣款合计</dt><dd>{{ money(detail.row.totalDeductions) }}</dd></div></dl></section><section v-if="detail.row.note" class="payroll-slip-note"><span>备注</span><p>{{ detail.row.note }}</p></section></template></el-drawer>
  </section>
</template>
