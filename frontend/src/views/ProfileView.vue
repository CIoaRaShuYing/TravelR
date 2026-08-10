<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Check, CreditCard } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { markProfileComplete, profileIncomplete } from '../session'

const router = useRouter()
const loading = ref(false)
const form = reactive({ personalName: '', bankCardNumber: '' })

async function load() {
  loading.value = true
  try {
    const profile = await api.getProfile()
    form.personalName = profile.personalName ?? ''
    form.bankCardNumber = profile.bankCardNumber ?? ''
  } catch (error) {
    ElMessage.error(api.message(error, '加载个人资料失败。'))
  } finally { loading.value = false }
}

async function save() {
  const personalName = form.personalName.trim()
  const bankCardNumber = form.bankCardNumber.replace(/\s/g, '')
  if (!personalName || !/^\d{16,19}$/.test(bankCardNumber)) {
    ElMessage.warning('请填写个人姓名和 16-19 位银行卡号。')
    return
  }
  loading.value = true
  try {
    await api.updateProfile({ personalName, bankCardNumber })
    form.bankCardNumber = bankCardNumber
    markProfileComplete()
    ElMessage.success('个人资料已保存。')
    await router.replace('/claims')
  } catch (error) {
    ElMessage.error(api.message(error, '保存个人资料失败。'))
  } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <section class="profile-page">
    <header class="page-header"><div><p class="eyebrow">PAYMENT PROFILE</p><h1>个人资料</h1><p>维护报销和餐补发放所需的收款信息。</p></div></header>
    <el-alert v-if="profileIncomplete" title="请先完成收款资料" description="未填写银行卡号时不能进入系统其他功能。报销及餐补将使用此处银行卡信息发放。" type="warning" :closable="false" show-icon />
    <div class="form-panel profile-form-panel" v-loading="loading">
      <div class="form-panel__heading"><el-icon><CreditCard /></el-icon><div><strong>收款账户</strong><span>请确认姓名与银行卡持有人一致</span></div></div>
      <el-form label-position="top" @submit.prevent="save">
        <el-form-item label="个人姓名" required><el-input v-model="form.personalName" maxlength="100" autocomplete="name" placeholder="银行卡持有人姓名" /></el-form-item>
        <el-form-item label="银行卡号" required><el-input v-model="form.bankCardNumber" maxlength="19" inputmode="numeric" autocomplete="cc-number" placeholder="16-19 位银行卡号" /></el-form-item>
        <el-button type="primary" :icon="Check" :loading="loading" native-type="submit">保存并进入系统</el-button>
      </el-form>
    </div>
  </section>
</template>
